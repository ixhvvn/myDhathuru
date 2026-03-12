using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Statements.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class StatementService : IStatementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public StatementService(
        ApplicationDbContext dbContext,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<AccountStatementDto> GetStatementAsync(Guid customerId, int year, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var openingBalance = await _dbContext.CustomerOpeningBalances
            .Where(x => x.CustomerId == customerId && x.Year == year)
            .Select(x => new
            {
                x.OpeningBalanceMvr,
                x.OpeningBalanceUsd
            })
            .FirstOrDefaultAsync(cancellationToken);

        var openingBalanceMvr = Round2(openingBalance?.OpeningBalanceMvr ?? 0m);
        var openingBalanceUsd = Round2(openingBalance?.OpeningBalanceUsd ?? 0m);

        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.DateIssued.Year == year)
            .OrderBy(x => x.DateIssued)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(x => x.Id).ToList();
        var invoiceIndex = invoices.ToDictionary(
            x => x.Id,
            x => new
            {
                x.InvoiceNo,
                Currency = NormalizeCurrency(x.Currency, "MVR")
            });

        var payments = await _dbContext.InvoicePayments
            .AsNoTracking()
            .Where(x => invoiceIds.Contains(x.InvoiceId) && x.PaymentDate.Year == year)
            .OrderBy(x => x.PaymentDate)
            .ToListAsync(cancellationToken);

        var hasMvrActivity = openingBalanceMvr > 0m
            || invoices.Any(x => NormalizeCurrency(x.Currency, "MVR") == "MVR")
            || payments.Any(x =>
            {
                if (!string.IsNullOrWhiteSpace(x.Currency))
                {
                    return NormalizeCurrency(x.Currency, "MVR") == "MVR";
                }

                return invoiceIndex.TryGetValue(x.InvoiceId, out var info) && info.Currency == "MVR";
            });

        var hasUsdActivity = openingBalanceUsd > 0m
            || invoices.Any(x => NormalizeCurrency(x.Currency, "MVR") == "USD")
            || payments.Any(x =>
            {
                if (!string.IsNullOrWhiteSpace(x.Currency))
                {
                    return NormalizeCurrency(x.Currency, "MVR") == "USD";
                }

                return invoiceIndex.TryGetValue(x.InvoiceId, out var info) && info.Currency == "USD";
            });

        var events = new List<StatementEvent>();

        if (hasMvrActivity || !hasUsdActivity)
        {
            events.Add(new StatementEvent
            {
                Date = new DateOnly(year, 1, 1),
                Description = "Previous Balance",
                Currency = "MVR",
                Amount = openingBalanceMvr,
                Payments = 0,
                Priority = 0
            });
        }

        if (hasUsdActivity)
        {
            events.Add(new StatementEvent
            {
                Date = new DateOnly(year, 1, 1),
                Description = "Previous Balance",
                Currency = "USD",
                Amount = openingBalanceUsd,
                Payments = 0,
                Priority = 0
            });
        }

        events.AddRange(invoices.Select(i => new StatementEvent
        {
            Date = i.DateIssued,
            Description = "Invoice",
            Reference = i.InvoiceNo,
            Currency = NormalizeCurrency(i.Currency, "MVR"),
            Amount = i.GrandTotal,
            Payments = 0,
            Priority = 1
        }));

        events.AddRange(payments.Select(p =>
        {
            invoiceIndex.TryGetValue(p.InvoiceId, out var invoiceInfo);
            var reference = invoiceInfo?.InvoiceNo;
            var paymentCurrency = NormalizeCurrency(p.Currency, invoiceInfo?.Currency ?? "MVR");

            return new StatementEvent
            {
                Date = p.PaymentDate,
                Description = "Payment",
                Reference = reference,
                Currency = paymentCurrency,
                Amount = 0,
                Payments = p.Amount,
                ReceivedOn = p.PaymentDate,
                Priority = 2
            };
        }));

        var ordered = events
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Currency)
            .ToList();

        var rows = new List<AccountStatementRowDto>();
        var runningMvr = 0m;
        var runningUsd = 0m;
        var index = 1;

        foreach (var evt in ordered)
        {
            if (evt.Currency == "USD")
            {
                runningUsd += evt.Amount;
                runningUsd -= evt.Payments;
            }
            else
            {
                runningMvr += evt.Amount;
                runningMvr -= evt.Payments;
            }

            rows.Add(new AccountStatementRowDto
            {
                Index = index++,
                Date = evt.Date,
                Description = evt.Description,
                Reference = evt.Reference,
                Currency = evt.Currency,
                Amount = evt.Amount,
                Payments = evt.Payments,
                ReceivedOn = evt.ReceivedOn,
                Balance = evt.Currency == "USD"
                    ? Round2(runningUsd)
                    : Round2(runningMvr)
            });
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var statementNo = $"{settings.StatementPrefix}/{year}/{customer.Id.ToString("N")[..4].ToUpperInvariant()}";

        var totalInvoiced = SumByCurrency(invoices.Select(x => (NormalizeCurrency(x.Currency, "MVR"), x.GrandTotal)));
        var totalReceived = SumByCurrency(payments.Select(x =>
        {
            invoiceIndex.TryGetValue(x.InvoiceId, out var info);
            var currency = NormalizeCurrency(x.Currency, info?.Currency ?? "MVR");
            return (currency, x.Amount);
        }));

        return new AccountStatementDto
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Year = year,
            StatementNo = statementNo,
            OpeningBalance = new StatementCurrencyTotalsDto
            {
                Mvr = openingBalanceMvr,
                Usd = openingBalanceUsd
            },
            TotalInvoiced = totalInvoiced,
            TotalReceived = totalReceived,
            TotalPending = new StatementCurrencyTotalsDto
            {
                Mvr = Round2(runningMvr),
                Usd = Round2(runningUsd)
            },
            Rows = rows
        };
    }

    public async Task SaveOpeningBalanceAsync(SaveOpeningBalanceRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var openingBalance = await _dbContext.CustomerOpeningBalances
            .FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId && x.Year == request.Year, cancellationToken);

        if (openingBalance is null)
        {
            openingBalance = new CustomerOpeningBalance
            {
                CustomerId = customer.Id,
                Year = request.Year,
                OpeningBalanceMvr = Round2(request.OpeningBalanceMvr),
                OpeningBalanceUsd = Round2(request.OpeningBalanceUsd),
                Notes = request.Notes?.Trim()
            };
            _dbContext.CustomerOpeningBalances.Add(openingBalance);
        }
        else
        {
            openingBalance.OpeningBalanceMvr = Round2(request.OpeningBalanceMvr);
            openingBalance.OpeningBalanceUsd = Round2(request.OpeningBalanceUsd);
            openingBalance.Notes = request.Notes?.Trim();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid customerId, int year, CancellationToken cancellationToken = default)
    {
        var statement = await GetStatementAsync(customerId, year, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";
        return _pdfExportService.BuildStatementPdf(statement, settings.CompanyName, companyInfo);
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static StatementCurrencyTotalsDto SumByCurrency(IEnumerable<(string Currency, decimal Amount)> rows)
    {
        var totals = new StatementCurrencyTotalsDto();
        foreach (var row in rows)
        {
            if (row.Currency == "USD")
            {
                totals.Usd += row.Amount;
            }
            else
            {
                totals.Mvr += row.Amount;
            }
        }

        totals.Mvr = Round2(totals.Mvr);
        totals.Usd = Round2(totals.Usd);
        return totals;
    }

    private static string NormalizeCurrency(string? currency, string fallbackCurrency)
    {
        if (string.Equals(currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
        {
            return "USD";
        }

        if (string.Equals(currency?.Trim(), "MVR", StringComparison.OrdinalIgnoreCase))
        {
            return "MVR";
        }

        return string.Equals(fallbackCurrency, "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "MVR";
    }

    private sealed class StatementEvent
    {
        public DateOnly Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string Currency { get; set; } = "MVR";
        public decimal Amount { get; set; }
        public decimal Payments { get; set; }
        public DateOnly? ReceivedOn { get; set; }
        public int Priority { get; set; }
    }
}
