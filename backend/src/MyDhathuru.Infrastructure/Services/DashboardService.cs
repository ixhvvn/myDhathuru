using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Dashboard.Dtos;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var currentMonthRange = GetMonthRange(utcNow, 0);
        var previousMonthRange = GetMonthRange(utcNow, -1);

        var currentMonthInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .CountAsync(x => x.DateIssued >= currentMonthRange.Start && x.DateIssued < currentMonthRange.EndExclusive, cancellationToken);

        var lastMonthInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .CountAsync(x => x.DateIssued >= previousMonthRange.Start && x.DateIssued < previousMonthRange.EndExclusive, cancellationToken);

        var currentMonthSales = await GetInvoiceTotalsByCurrencyAsync(currentMonthRange, cancellationToken);
        var lastMonthSales = await GetInvoiceTotalsByCurrencyAsync(previousMonthRange, cancellationToken);

        var currentMonthPending = await GetPendingTotalsByCurrencyAsync(currentMonthRange, cancellationToken);
        var lastMonthPending = await GetPendingTotalsByCurrencyAsync(previousMonthRange, cancellationToken);

        var currentMonthDeliveryNotes = await _dbContext.DeliveryNotes
            .AsNoTracking()
            .CountAsync(x => x.Date >= currentMonthRange.Start && x.Date < currentMonthRange.EndExclusive, cancellationToken);

        var lastMonthDeliveryNotes = await _dbContext.DeliveryNotes
            .AsNoTracking()
            .CountAsync(x => x.Date >= previousMonthRange.Start && x.Date < previousMonthRange.EndExclusive, cancellationToken);

        var currentMonthStartUtc = ToUtcMonthStart(currentMonthRange.Start);
        var currentMonthEndUtc = ToUtcMonthStart(currentMonthRange.EndExclusive);
        var lastMonthStartUtc = ToUtcMonthStart(previousMonthRange.Start);
        var lastMonthEndUtc = ToUtcMonthStart(previousMonthRange.EndExclusive);

        var currentMonthNewCustomers = await _dbContext.Customers
            .AsNoTracking()
            .CountAsync(x => x.CreatedAt >= currentMonthStartUtc && x.CreatedAt < currentMonthEndUtc, cancellationToken);

        var lastMonthNewCustomers = await _dbContext.Customers
            .AsNoTracking()
            .CountAsync(x => x.CreatedAt >= lastMonthStartUtc && x.CreatedAt < lastMonthEndUtc, cancellationToken);

        var currentMonthPayroll = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(x => x.Year == currentMonthRange.Start.Year && x.Month == currentMonthRange.Start.Month)
            .SumAsync(x => (decimal?)x.TotalNetPayable, cancellationToken) ?? 0m;

        var lastMonthPayroll = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(x => x.Year == previousMonthRange.Start.Year && x.Month == previousMonthRange.Start.Month)
            .SumAsync(x => (decimal?)x.TotalNetPayable, cancellationToken) ?? 0m;

        return new DashboardSummaryDto
        {
            CurrentMonthInvoices = currentMonthInvoices,
            LastMonthInvoices = lastMonthInvoices,
            InvoicesTrend = BuildTrend(currentMonthInvoices, lastMonthInvoices),

            CurrentMonthSales = currentMonthSales,
            LastMonthSales = lastMonthSales,
            SalesTrend = new DashboardCurrencyTrendDto
            {
                Mvr = BuildTrend(currentMonthSales.Mvr, lastMonthSales.Mvr),
                Usd = BuildTrend(currentMonthSales.Usd, lastMonthSales.Usd)
            },

            CurrentMonthPending = currentMonthPending,
            LastMonthPending = lastMonthPending,
            PendingTrend = new DashboardCurrencyTrendDto
            {
                Mvr = BuildTrend(currentMonthPending.Mvr, lastMonthPending.Mvr),
                Usd = BuildTrend(currentMonthPending.Usd, lastMonthPending.Usd)
            },

            CurrentMonthDeliveryNotes = currentMonthDeliveryNotes,
            LastMonthDeliveryNotes = lastMonthDeliveryNotes,
            DeliveryNotesTrend = BuildTrend(currentMonthDeliveryNotes, lastMonthDeliveryNotes),

            CurrentMonthNewCustomers = currentMonthNewCustomers,
            LastMonthNewCustomers = lastMonthNewCustomers,
            NewCustomersTrend = BuildTrend(currentMonthNewCustomers, lastMonthNewCustomers),

            CurrentMonthPayroll = currentMonthPayroll,
            LastMonthPayroll = lastMonthPayroll,
            PayrollTrend = BuildTrend(currentMonthPayroll, lastMonthPayroll)
        };
    }

    public async Task<DashboardAnalyticsDto> GetAnalyticsAsync(int topCustomersLimit = 5, CancellationToken cancellationToken = default)
    {
        var safeTopCustomerLimit = Math.Clamp(topCustomersLimit, 1, 10);

        var summary = await GetSummaryAsync(cancellationToken);
        var topCustomers = await GetTopCustomersAsync(safeTopCustomerLimit, cancellationToken);
        var salesLast6Months = await GetSalesLastSixMonthsAsync(cancellationToken);
        var vesselSales = await GetVesselSalesAsync(cancellationToken);

        return new DashboardAnalyticsDto
        {
            Summary = summary,
            TopCustomers = topCustomers,
            SalesLast6Months = salesLast6Months,
            VesselSales = vesselSales
        };
    }

    private async Task<DashboardCurrencyAmountDto> GetInvoiceTotalsByCurrencyAsync(
        MonthRange monthRange,
        CancellationToken cancellationToken)
    {
        var grouped = await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.DateIssued >= monthRange.Start && x.DateIssued < monthRange.EndExclusive)
            .GroupBy(x => x.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync(cancellationToken);

        return MapCurrencyTotals(grouped.Select(x => (x.Currency, x.Total)));
    }

    private async Task<DashboardCurrencyAmountDto> GetPendingTotalsByCurrencyAsync(
        MonthRange monthRange,
        CancellationToken cancellationToken)
    {
        var grouped = await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.DateIssued >= monthRange.Start && x.DateIssued < monthRange.EndExclusive)
            .GroupBy(x => x.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.Balance) })
            .ToListAsync(cancellationToken);

        return MapCurrencyTotals(grouped.Select(x => (x.Currency, x.Total)));
    }

    private async Task<IReadOnlyCollection<DashboardTopCustomerDto>> GetTopCustomersAsync(
        int topCustomersLimit,
        CancellationToken cancellationToken)
    {
        var overallTotals = await _dbContext.Invoices
            .AsNoTracking()
            .GroupBy(x => x.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync(cancellationToken);

        var totalByCurrency = MapCurrencyTotals(overallTotals.Select(x => (x.Currency, x.Total)));

        var rows = await _dbContext.Invoices
            .AsNoTracking()
            .GroupBy(x => new { x.CustomerId, x.Customer.Name })
            .Select(g => new
            {
                g.Key.CustomerId,
                CustomerName = g.Key.Name,
                SalesMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.GrandTotal),
                SalesUsd = g.Where(x => x.Currency == "USD").Sum(x => x.GrandTotal),
                InvoiceCount = g.Count()
            })
            .OrderByDescending(x => x.SalesMvr)
            .ThenByDescending(x => x.SalesUsd)
            .ThenByDescending(x => x.InvoiceCount)
            .ThenBy(x => x.CustomerName)
            .Take(topCustomersLimit)
            .ToListAsync(cancellationToken);

        var rank = 1;
        return rows.Select(row => new DashboardTopCustomerDto
        {
            Rank = rank++,
            CustomerId = row.CustomerId,
            CustomerName = row.CustomerName,
            SalesMvr = row.SalesMvr,
            SalesUsd = row.SalesUsd,
            InvoiceCount = row.InvoiceCount,
            ContributionMvrPercentage = totalByCurrency.Mvr <= 0m
                ? 0m
                : Math.Round((row.SalesMvr / totalByCurrency.Mvr) * 100m, 2),
            ContributionUsdPercentage = totalByCurrency.Usd <= 0m
                ? 0m
                : Math.Round((row.SalesUsd / totalByCurrency.Usd) * 100m, 2),
            Initials = BuildInitials(row.CustomerName)
        }).ToList();
    }

    private async Task<IReadOnlyCollection<DashboardMonthlySalesDto>> GetSalesLastSixMonthsAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var currentMonthStart = new DateOnly(utcNow.Year, utcNow.Month, 1);
        var startMonth = currentMonthStart.AddMonths(-5);
        var nextMonthStart = currentMonthStart.AddMonths(1);

        var groupedSales = await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.DateIssued >= startMonth && x.DateIssued < nextMonthStart)
            .GroupBy(x => new { x.DateIssued.Year, x.DateIssued.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                SalesMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.GrandTotal),
                SalesUsd = g.Where(x => x.Currency == "USD").Sum(x => x.GrandTotal)
            })
            .ToListAsync(cancellationToken);

        var salesLookup = groupedSales.ToDictionary(x => (x.Year, x.Month), x => (x.SalesMvr, x.SalesUsd));
        var timeline = new List<DashboardMonthlySalesDto>(6);

        for (var offset = 0; offset < 6; offset++)
        {
            var month = startMonth.AddMonths(offset);
            salesLookup.TryGetValue((month.Year, month.Month), out var totals);

            timeline.Add(new DashboardMonthlySalesDto
            {
                Year = month.Year,
                Month = month.Month,
                Label = month.ToDateTime(TimeOnly.MinValue).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                SalesMvr = totals.SalesMvr,
                SalesUsd = totals.SalesUsd
            });
        }

        return timeline;
    }

    private async Task<IReadOnlyCollection<DashboardVesselSalesDto>> GetVesselSalesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Invoices
            .AsNoTracking()
            .Select(x => new
            {
                VesselId = x.CourierVesselId ?? (x.DeliveryNote != null ? x.DeliveryNote.VesselId : null),
                VesselName =
                    x.CourierVessel != null
                        ? x.CourierVessel.Name
                        : x.DeliveryNote != null && x.DeliveryNote.Vessel != null
                            ? x.DeliveryNote.Vessel.Name
                            : "Unassigned Vessel",
                x.Currency,
                x.GrandTotal
            })
            .GroupBy(x => new { x.VesselId, x.VesselName })
            .Select(g => new
            {
                g.Key.VesselId,
                g.Key.VesselName,
                SalesMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.GrandTotal),
                SalesUsd = g.Where(x => x.Currency == "USD").Sum(x => x.GrandTotal)
            })
            .OrderByDescending(x => x.SalesMvr)
            .ThenByDescending(x => x.SalesUsd)
            .ThenBy(x => x.VesselName)
            .ToListAsync(cancellationToken);

        var totalMvr = rows.Sum(x => x.SalesMvr);
        var totalUsd = rows.Sum(x => x.SalesUsd);

        return rows.Select(row => new DashboardVesselSalesDto
        {
            VesselId = row.VesselId,
            VesselName = row.VesselName,
            SalesMvr = row.SalesMvr,
            SalesUsd = row.SalesUsd,
            ContributionMvrPercentage = totalMvr <= 0m
                ? 0m
                : Math.Round((row.SalesMvr / totalMvr) * 100m, 2),
            ContributionUsdPercentage = totalUsd <= 0m
                ? 0m
                : Math.Round((row.SalesUsd / totalUsd) * 100m, 2)
        }).ToList();
    }

    private static DashboardCurrencyAmountDto MapCurrencyTotals(IEnumerable<(string Currency, decimal Total)> totals)
    {
        var result = new DashboardCurrencyAmountDto();
        foreach (var row in totals)
        {
            var currency = (row.Currency ?? string.Empty).Trim().ToUpperInvariant();
            if (currency == "USD")
            {
                result.Usd += row.Total;
            }
            else
            {
                result.Mvr += row.Total;
            }
        }

        result.Mvr = Math.Round(result.Mvr, 2, MidpointRounding.AwayFromZero);
        result.Usd = Math.Round(result.Usd, 2, MidpointRounding.AwayFromZero);
        return result;
    }

    private static DashboardTrendDto BuildTrend(decimal current, decimal last)
    {
        current = Math.Round(current, 2, MidpointRounding.AwayFromZero);
        last = Math.Round(last, 2, MidpointRounding.AwayFromZero);

        if (last <= 0m)
        {
            return current <= 0m
                ? new DashboardTrendDto
                {
                    Percentage = null,
                    Direction = "neutral",
                    Label = "No activity"
                }
                : new DashboardTrendDto
                {
                    Percentage = null,
                    Direction = "up",
                    Label = "New this month"
                };
        }

        var percentage = Math.Round(((current - last) / last) * 100m, 1, MidpointRounding.AwayFromZero);
        if (Math.Abs(percentage) < 0.05m)
        {
            return new DashboardTrendDto
            {
                Percentage = 0m,
                Direction = "neutral",
                Label = "No change"
            };
        }

        if (current <= 0m)
        {
            return new DashboardTrendDto
            {
                Percentage = -100m,
                Direction = "down",
                Label = "Down 100%"
            };
        }

        var isUp = percentage > 0m;
        var label = $"{(isUp ? "Up" : "Down")} {FormatTrendPercentage(Math.Abs(percentage))}%";
        return new DashboardTrendDto
        {
            Percentage = percentage,
            Direction = isUp ? "up" : "down",
            Label = label
        };
    }

    private static DashboardTrendDto BuildTrend(int current, int last) => BuildTrend((decimal)current, last);

    private static string BuildInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "--";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "--";
        }

        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0].ToUpperInvariant();
        }

        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
    }

    private static MonthRange GetMonthRange(DateTime utcNow, int monthOffset)
    {
        var monthStart = new DateOnly(utcNow.Year, utcNow.Month, 1).AddMonths(monthOffset);
        return new MonthRange(monthStart, monthStart.AddMonths(1));
    }

    private static DateTimeOffset ToUtcMonthStart(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string FormatTrendPercentage(decimal value)
    {
        var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        if (rounded == Math.Truncate(rounded))
        {
            return rounded.ToString("0", CultureInfo.InvariantCulture);
        }

        return rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private sealed record MonthRange(DateOnly Start, DateOnly EndExclusive);
}
