using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PurchaseOrders.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Helpers;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentTenantService _currentTenantService;

    public PurchaseOrderService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        INotificationService notificationService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _notificationService = notificationService;
        _currentTenantService = currentTenantService;
    }

    public async Task<PagedResult<PurchaseOrderListItemDto>> GetPagedAsync(PurchaseOrderListQuery query, CancellationToken cancellationToken = default)
    {
        var purchaseOrders = _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.CourierVessel)
            .Where(x =>
                (query.DateFrom == null || x.DateIssued >= query.DateFrom)
                && (query.DateTo == null || x.DateIssued <= query.DateTo)
                && (query.SupplierId == null || x.SupplierId == query.SupplierId)
                && (string.IsNullOrWhiteSpace(query.Search)
                    || x.PurchaseOrderNo.ToLower().Contains(query.Search.ToLower())
                    || x.Supplier.Name.ToLower().Contains(query.Search.ToLower())));

        purchaseOrders = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? purchaseOrders.OrderBy(x => x.DateIssued).ThenBy(x => x.CreatedAt)
            : purchaseOrders.OrderByDescending(x => x.DateIssued).ThenByDescending(x => x.CreatedAt);

        return await purchaseOrders.Select(x => new PurchaseOrderListItemDto
            {
                Id = x.Id,
                PurchaseOrderNo = x.PurchaseOrderNo,
                Supplier = x.Supplier.Name,
                CourierId = x.CourierVesselId,
                CourierName = x.CourierVessel != null ? x.CourierVessel.Name : null,
                Currency = x.Currency,
                Amount = x.GrandTotal,
                DateIssued = x.DateIssued,
                RequiredDate = x.RequiredDate,
                EmailStatus = x.EmailStatus,
                LastEmailedAt = x.LastEmailedAt
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<PurchaseOrderDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.CourierVessel)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return purchaseOrder is null ? null : MapPurchaseOrder(purchaseOrder);
    }

    public async Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        Vessel? courier = null;
        if (request.CourierId.HasValue)
        {
            courier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var dateIssued = request.DateIssued;
        var requiredDate = request.RequiredDate ?? dateIssued.AddDays(settings.DefaultDueDays);
        var purchaseOrderNo = await _documentNumberService.GenerateAsync(DocumentType.PurchaseOrder, dateIssued, cancellationToken);

        var purchaseOrder = new PurchaseOrder
        {
            PurchaseOrderNo = purchaseOrderNo,
            SupplierId = supplier.Id,
            CourierVesselId = courier?.Id,
            DateIssued = dateIssued,
            RequiredDate = requiredDate,
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            TaxRate = ResolveTaxRate(request.TaxRate, settings),
            Notes = request.Notes?.Trim()
        };

        foreach (var item in request.Items)
        {
            purchaseOrder.Items.Add(new PurchaseOrderItem
            {
                Description = item.Description.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate
            });
        }

        RecalculatePurchaseOrder(purchaseOrder);
        _dbContext.PurchaseOrders.Add(purchaseOrder);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(purchaseOrder.Id, cancellationToken))!;
    }

    public async Task<PurchaseOrderDetailDto> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        Vessel? courier = null;
        if (request.CourierId.HasValue)
        {
            courier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        purchaseOrder.SupplierId = supplier.Id;
        purchaseOrder.CourierVesselId = courier?.Id;
        purchaseOrder.DateIssued = request.DateIssued;
        purchaseOrder.RequiredDate = request.RequiredDate ?? request.DateIssued.AddDays(settings.DefaultDueDays);
        purchaseOrder.Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        purchaseOrder.TaxRate = ResolveTaxRate(request.TaxRate, settings);
        purchaseOrder.Notes = request.Notes?.Trim();
        ResetEmailStatus(purchaseOrder);

        foreach (var item in purchaseOrder.Items.ToList())
        {
            _dbContext.PurchaseOrderItems.Remove(item);
        }

        var replacementItems = BuildPurchaseOrderItems(request.Items, purchaseOrder.Id);
        _dbContext.PurchaseOrderItems.AddRange(replacementItems);

        RecalculatePurchaseOrder(purchaseOrder, replacementItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(purchaseOrder.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.PurchaseOrderItems
            .Where(x => x.PurchaseOrderId == purchaseOrder.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PurchaseOrders
            .Where(x => x.Id == purchaseOrder.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SendEmailAsync(Guid id, SendPurchaseOrderEmailRequest request, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await _dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.CourierVessel)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");

        var recipientEmail = purchaseOrder.Supplier.Email?.Trim();
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new AppException("Supplier email is required before sending this purchase order.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var body = DocumentEmailTemplateHelper.RenderPurchaseOrder(
            string.IsNullOrWhiteSpace(request.Body) ? settings.PurchaseOrderEmailBodyTemplate : request.Body,
            settings.CompanyName);
        var ccEmail = string.IsNullOrWhiteSpace(request.CcEmail) ? null : request.CcEmail.Trim();
        var subject = $"PO {purchaseOrder.PurchaseOrderNo} from {settings.CompanyName}";
        var pdfBytes = BuildPurchaseOrderPdf(MapPurchaseOrder(purchaseOrder), settings);

        await _notificationService.SendDocumentEmailAsync(
            recipientEmail,
            ccEmail,
            subject,
            body,
            pdfBytes,
            $"{purchaseOrder.PurchaseOrderNo}.pdf",
            settings.CompanyName,
            settings.CompanyEmail,
            cancellationToken);

        purchaseOrder.EmailStatus = DocumentEmailStatus.Emailed;
        purchaseOrder.LastEmailedAt = DateTimeOffset.UtcNow;
        purchaseOrder.LastEmailedTo = recipientEmail;
        purchaseOrder.LastEmailedCc = ccEmail;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");

        var settings = await GetTenantSettingsAsync(cancellationToken);
        return BuildPurchaseOrderPdf(purchaseOrder, settings);
    }

    private static List<PurchaseOrderItem> BuildPurchaseOrderItems(IEnumerable<PurchaseOrderItemInputDto> items, Guid? purchaseOrderId = null)
    {
        return items.Select(item => new PurchaseOrderItem
        {
            PurchaseOrderId = purchaseOrderId ?? Guid.Empty,
            Description = item.Description.Trim(),
            Qty = item.Qty,
            Rate = item.Rate,
            Total = item.Qty * item.Rate
        }).ToList();
    }

    private static void RecalculatePurchaseOrder(PurchaseOrder purchaseOrder, IEnumerable<PurchaseOrderItem>? items = null)
    {
        var sourceItems = items ?? purchaseOrder.Items;
        purchaseOrder.Subtotal = Math.Round(sourceItems.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero);
        purchaseOrder.TaxAmount = Math.Round(purchaseOrder.Subtotal * purchaseOrder.TaxRate, 2, MidpointRounding.AwayFromZero);
        purchaseOrder.GrandTotal = Math.Round(purchaseOrder.Subtotal + purchaseOrder.TaxAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ResolveTaxRate(decimal? requestedTaxRate, TenantSettings settings)
    {
        if (!settings.IsTaxApplicable)
        {
            return 0m;
        }

        return requestedTaxRate ?? settings.DefaultTaxRate;
    }

    private static PurchaseOrderDetailDto MapPurchaseOrder(PurchaseOrder purchaseOrder)
    {
        return new PurchaseOrderDetailDto
        {
            Id = purchaseOrder.Id,
            PurchaseOrderNo = purchaseOrder.PurchaseOrderNo,
            SupplierId = purchaseOrder.SupplierId,
            SupplierName = purchaseOrder.Supplier.Name,
            SupplierTinNumber = purchaseOrder.Supplier.TinNumber,
            SupplierContactNumber = purchaseOrder.Supplier.ContactNumber,
            SupplierEmail = purchaseOrder.Supplier.Email,
            CourierId = purchaseOrder.CourierVesselId,
            CourierName = purchaseOrder.CourierVessel?.Name,
            DateIssued = purchaseOrder.DateIssued,
            RequiredDate = purchaseOrder.RequiredDate,
            Currency = purchaseOrder.Currency,
            Subtotal = purchaseOrder.Subtotal,
            TaxRate = purchaseOrder.TaxRate,
            TaxAmount = purchaseOrder.TaxAmount,
            GrandTotal = purchaseOrder.GrandTotal,
            EmailStatus = purchaseOrder.EmailStatus,
            LastEmailedAt = purchaseOrder.LastEmailedAt,
            Notes = purchaseOrder.Notes,
            Items = purchaseOrder.Items.OrderBy(x => x.CreatedAt).Select(item => new PurchaseOrderItemDto
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

    private static void ResetEmailStatus(PurchaseOrder purchaseOrder)
    {
        purchaseOrder.EmailStatus = DocumentEmailStatus.Pending;
        purchaseOrder.LastEmailedAt = null;
        purchaseOrder.LastEmailedTo = null;
        purchaseOrder.LastEmailedCc = null;
    }

    private byte[] BuildPurchaseOrderPdf(PurchaseOrderDetailDto purchaseOrder, TenantSettings settings)
    {
        var companyInfo = settings.BuildCompanyInfo(includeBusinessRegistration: true);
        return _pdfExportService.BuildPurchaseOrderPdf(purchaseOrder, settings.CompanyName, companyInfo, settings.LogoUrl, settings.IsTaxApplicable);
    }
}
