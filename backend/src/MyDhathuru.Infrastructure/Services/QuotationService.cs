using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Quotations.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class QuotationService : IQuotationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public QuotationService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResult<QuotationListItemDto>> GetPagedAsync(QuotationListQuery query, CancellationToken cancellationToken = default)
    {
        var quotations = _dbContext.Quotations
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CourierVessel)
            .Where(x =>
                (query.DateFrom == null || x.DateIssued >= query.DateFrom)
                && (query.DateTo == null || x.DateIssued <= query.DateTo)
                && (query.CustomerId == null || x.CustomerId == query.CustomerId)
                && (string.IsNullOrWhiteSpace(query.Search)
                    || x.QuotationNo.ToLower().Contains(query.Search.ToLower())
                    || x.Customer.Name.ToLower().Contains(query.Search.ToLower())));

        quotations = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? quotations.OrderBy(x => x.DateIssued).ThenBy(x => x.CreatedAt)
            : quotations.OrderByDescending(x => x.DateIssued).ThenByDescending(x => x.CreatedAt);

        return await quotations.Select(x => new QuotationListItemDto
            {
                Id = x.Id,
                QuotationNo = x.QuotationNo,
                Customer = x.Customer.Name,
                CourierId = x.CourierVesselId,
                CourierName = x.CourierVessel != null ? x.CourierVessel.Name : null,
                Currency = x.Currency,
                Amount = x.GrandTotal,
                DateIssued = x.DateIssued,
                ValidUntil = x.ValidUntil,
                ConvertedInvoiceId = x.ConvertedInvoice != null ? x.ConvertedInvoice.Id : null,
                ConvertedInvoiceNo = x.ConvertedInvoice != null ? x.ConvertedInvoice.InvoiceNo : null
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<QuotationDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CourierVessel)
            .Include(x => x.ConvertedInvoice)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return quotation is null ? null : MapQuotation(quotation);
    }

    public async Task<QuotationDetailDto> CreateAsync(CreateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        Vessel? courier = null;
        if (request.CourierId.HasValue)
        {
            courier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var dateIssued = request.DateIssued;
        var validUntil = request.ValidUntil ?? dateIssued.AddDays(settings.DefaultDueDays);
        var quotationNo = await _documentNumberService.GenerateAsync(DocumentType.Quotation, dateIssued, cancellationToken);

        var quotation = new Quotation
        {
            QuotationNo = quotationNo,
            CustomerId = customer.Id,
            CourierVesselId = courier?.Id,
            PoNumber = request.PoNumber?.Trim(),
            DateIssued = dateIssued,
            ValidUntil = validUntil,
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            TaxRate = ResolveTaxRate(request.TaxRate, settings),
            Notes = request.Notes?.Trim()
        };

        foreach (var item in request.Items)
        {
            quotation.Items.Add(new QuotationItem
            {
                Description = item.Description.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate
            });
        }

        RecalculateQuotation(quotation);
        _dbContext.Quotations.Add(quotation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(quotation.Id, cancellationToken))!;
    }

    public async Task<QuotationDetailDto> UpdateAsync(Guid id, UpdateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found.");

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        Vessel? courier = null;
        if (request.CourierId.HasValue)
        {
            courier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        quotation.CustomerId = customer.Id;
        quotation.CourierVesselId = courier?.Id;
        quotation.PoNumber = request.PoNumber?.Trim();
        quotation.DateIssued = request.DateIssued;
        quotation.ValidUntil = request.ValidUntil ?? request.DateIssued.AddDays(settings.DefaultDueDays);
        quotation.Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        quotation.TaxRate = ResolveTaxRate(request.TaxRate, settings);
        quotation.Notes = request.Notes?.Trim();

        foreach (var item in quotation.Items.ToList())
        {
            _dbContext.QuotationItems.Remove(item);
        }

        var replacementItems = BuildQuotationItems(request.Items, quotation.Id);
        _dbContext.QuotationItems.AddRange(replacementItems);

        RecalculateQuotation(quotation, replacementItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(quotation.Id, cancellationToken))!;
    }

    public async Task<QuotationConversionResultDto> ConvertToSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(x => x.Items)
            .Include(x => x.ConvertedInvoice)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found.");

        if (quotation.ConvertedInvoice is not null)
        {
            return new QuotationConversionResultDto
            {
                InvoiceId = quotation.ConvertedInvoice.Id,
                InvoiceNo = quotation.ConvertedInvoice.InvoiceNo,
                AlreadyConverted = true
            };
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var conversionDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var invoiceNo = await _documentNumberService.GenerateAsync(DocumentType.Invoice, conversionDate, cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNo = invoiceNo,
            CustomerId = quotation.CustomerId,
            QuotationId = quotation.Id,
            CourierVesselId = quotation.CourierVesselId,
            PoNumber = quotation.PoNumber,
            DateIssued = conversionDate,
            DateDue = conversionDate.AddDays(settings.DefaultDueDays),
            Currency = NormalizeCurrency(quotation.Currency, settings.DefaultCurrency),
            TaxRate = ResolveTaxRate(quotation.TaxRate, settings),
            Notes = quotation.Notes?.Trim()
        };

        foreach (var item in quotation.Items.OrderBy(x => x.CreatedAt))
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description,
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Total
            });
        }

        RecalculateInvoice(invoice);
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new QuotationConversionResultDto
        {
            InvoiceId = invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            AlreadyConverted = false
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.QuotationItems
            .Where(x => x.QuotationId == quotation.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Quotations
            .Where(x => x.Id == quotation.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Quotation not found.");

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = settings.BuildCompanyInfo(includeBusinessRegistration: true);
        return _pdfExportService.BuildQuotationPdf(quotation, settings.CompanyName, companyInfo, settings.LogoUrl, settings.IsTaxApplicable);
    }

    private static List<QuotationItem> BuildQuotationItems(IEnumerable<QuotationItemInputDto> items, Guid? quotationId = null)
    {
        return items.Select(item => new QuotationItem
        {
            QuotationId = quotationId ?? Guid.Empty,
            Description = item.Description.Trim(),
            Qty = item.Qty,
            Rate = item.Rate,
            Total = item.Qty * item.Rate
        }).ToList();
    }

    private static void RecalculateQuotation(Quotation quotation, IEnumerable<QuotationItem>? items = null)
    {
        var sourceItems = items ?? quotation.Items;
        quotation.Subtotal = Math.Round(sourceItems.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero);
        quotation.TaxAmount = Math.Round(quotation.Subtotal * quotation.TaxRate, 2, MidpointRounding.AwayFromZero);
        quotation.GrandTotal = Math.Round(quotation.Subtotal + quotation.TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveTaxRate(decimal? requestedTaxRate, TenantSettings settings)
    {
        if (!settings.IsTaxApplicable)
        {
            return 0m;
        }

        return requestedTaxRate ?? settings.DefaultTaxRate;
    }

    private static QuotationDetailDto MapQuotation(Quotation quotation)
    {
        return new QuotationDetailDto
        {
            Id = quotation.Id,
            QuotationNo = quotation.QuotationNo,
            PoNumber = quotation.PoNumber,
            CustomerId = quotation.CustomerId,
            CustomerName = quotation.Customer.Name,
            CustomerTinNumber = quotation.Customer.TinNumber,
            CustomerPhone = quotation.Customer.Phone,
            CustomerEmail = quotation.Customer.Email,
            CourierId = quotation.CourierVesselId,
            CourierName = quotation.CourierVessel?.Name,
            DateIssued = quotation.DateIssued,
            ValidUntil = quotation.ValidUntil,
            Currency = quotation.Currency,
            Subtotal = quotation.Subtotal,
            TaxRate = quotation.TaxRate,
            TaxAmount = quotation.TaxAmount,
            GrandTotal = quotation.GrandTotal,
            Notes = quotation.Notes,
            ConvertedInvoiceId = quotation.ConvertedInvoice?.Id,
            ConvertedInvoiceNo = quotation.ConvertedInvoice?.InvoiceNo,
            Items = quotation.Items.OrderBy(x => x.CreatedAt).Select(item => new QuotationItemDto
            {
                Id = item.Id,
                Description = item.Description,
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Total
            }).ToList()
        };
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string NormalizeCurrency(string? requestedCurrency, string fallbackCurrency)
    {
        var currency = string.IsNullOrWhiteSpace(requestedCurrency)
            ? fallbackCurrency
            : requestedCurrency.Trim();

        if (!Enum.TryParse<CurrencyCode>(currency, true, out var parsed))
        {
            throw new AppException("Currency must be MVR or USD.");
        }

        return parsed.ToString().ToUpperInvariant();
    }

    private static void RecalculateInvoice(Invoice invoice)
    {
        invoice.Subtotal = Math.Round(invoice.Items.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero);
        invoice.TaxAmount = Math.Round(invoice.Subtotal * invoice.TaxRate, 2, MidpointRounding.AwayFromZero);
        invoice.GrandTotal = Math.Round(invoice.Subtotal + invoice.TaxAmount, 2, MidpointRounding.AwayFromZero);
        invoice.AmountPaid = 0;
        invoice.Balance = invoice.GrandTotal;
        invoice.PaymentStatus = PaymentStatus.Unpaid;
    }
}
