using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.ReceivedInvoices.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class ReceivedInvoiceService : IReceivedInvoiceService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;

    public ReceivedInvoiceService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<ReceivedInvoiceListItemDto>> GetPagedAsync(ReceivedInvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var invoices = BuildQuery(query, today)
            .Select(x => new ReceivedInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                SupplierId = x.SupplierId,
                SupplierName = x.SupplierName,
                InvoiceDate = x.InvoiceDate,
                DueDate = x.DueDate,
                Currency = x.Currency,
                TotalAmount = x.TotalAmount,
                BalanceDue = x.BalanceDue,
                PaymentStatus = x.PaymentStatus,
                ApprovalStatus = x.ApprovalStatus,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                IsTaxClaimable = x.IsTaxClaimable,
                IsOverdue = x.DueDate < today && x.BalanceDue > 0,
                AttachmentCount = x.Attachments.Count
            });

        return await invoices.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ReceivedInvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Items)
            .Include(x => x.Payments)
                .ThenInclude(x => x.PaymentVoucher)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return invoice is null ? null : Map(invoice);
    }

    public async Task<ReceivedInvoiceDetailDto> CreateAsync(CreateReceivedInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");
        var expenseCategory = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        await EnsureInvoiceNumberUniqueAsync(request.InvoiceNumber.Trim(), null, cancellationToken);

        var invoice = new ReceivedInvoice
        {
            InvoiceNumber = request.InvoiceNumber.Trim(),
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            SupplierTin = supplier.TinNumber,
            SupplierContactNumber = supplier.ContactNumber,
            SupplierEmail = supplier.Email,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate ?? request.InvoiceDate.AddDays(settings.DefaultDueDays),
            Outlet = request.Outlet?.Trim(),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            DiscountAmount = Round2(request.DiscountAmount),
            PaymentMethod = request.PaymentMethod,
            ReceiptReference = request.ReceiptReference?.Trim(),
            SettlementReference = request.SettlementReference?.Trim(),
            BankName = request.BankName?.Trim(),
            BankAccountDetails = request.BankAccountDetails?.Trim(),
            MiraTaxableActivityNumber = string.IsNullOrWhiteSpace(request.MiraTaxableActivityNumber)
                ? settings.TaxableActivityNumber
                : request.MiraTaxableActivityNumber.Trim(),
            RevenueCapitalClassification = request.RevenueCapitalClassification,
            ExpenseCategoryId = expenseCategory.Id,
            IsTaxClaimable = settings.IsInputTaxClaimEnabled && request.IsTaxClaimable,
            ApprovalStatus = request.ApprovalStatus
        };

        ApplyApproval(invoice, request.ApprovalStatus);
        ApplyItems(invoice, request.Items, settings, request.GstRate);

        _dbContext.ReceivedInvoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ReceivedInvoiceCreated,
            nameof(ReceivedInvoice),
            invoice.Id.ToString(),
            invoice.InvoiceNumber,
            new { invoice.SupplierName, invoice.TotalAmount, invoice.Currency, invoice.PaymentStatus },
            cancellationToken);

        return (await GetByIdAsync(invoice.Id, cancellationToken))!;
    }

    public async Task<ReceivedInvoiceDetailDto> UpdateAsync(Guid id, UpdateReceivedInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var invoice = await _dbContext.ReceivedInvoices
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Received invoice not found.");

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");
        var expenseCategory = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        await EnsureInvoiceNumberUniqueAsync(request.InvoiceNumber.Trim(), id, cancellationToken);

        invoice.InvoiceNumber = request.InvoiceNumber.Trim();
        invoice.SupplierId = supplier.Id;
        invoice.SupplierName = supplier.Name;
        invoice.SupplierTin = supplier.TinNumber;
        invoice.SupplierContactNumber = supplier.ContactNumber;
        invoice.SupplierEmail = supplier.Email;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate ?? request.InvoiceDate.AddDays(settings.DefaultDueDays);
        invoice.Outlet = request.Outlet?.Trim();
        invoice.Description = request.Description?.Trim();
        invoice.Notes = request.Notes?.Trim();
        invoice.Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        invoice.DiscountAmount = Round2(request.DiscountAmount);
        invoice.PaymentMethod = request.PaymentMethod;
        invoice.ReceiptReference = request.ReceiptReference?.Trim();
        invoice.SettlementReference = request.SettlementReference?.Trim();
        invoice.BankName = request.BankName?.Trim();
        invoice.BankAccountDetails = request.BankAccountDetails?.Trim();
        invoice.MiraTaxableActivityNumber = string.IsNullOrWhiteSpace(request.MiraTaxableActivityNumber)
            ? settings.TaxableActivityNumber
            : request.MiraTaxableActivityNumber.Trim();
        invoice.RevenueCapitalClassification = request.RevenueCapitalClassification;
        invoice.ExpenseCategoryId = expenseCategory.Id;
        invoice.IsTaxClaimable = settings.IsInputTaxClaimEnabled && request.IsTaxClaimable;
        invoice.ApprovalStatus = request.ApprovalStatus;

        ApplyApproval(invoice, request.ApprovalStatus);

        foreach (var item in invoice.Items.ToList())
        {
            _dbContext.ReceivedInvoiceItems.Remove(item);
        }

        ApplyItems(invoice, request.Items, settings, request.GstRate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ReceivedInvoiceUpdated,
            nameof(ReceivedInvoice),
            invoice.Id.ToString(),
            invoice.InvoiceNumber,
            new { invoice.SupplierName, invoice.TotalAmount, invoice.Currency, invoice.PaymentStatus },
            cancellationToken);

        return (await GetByIdAsync(invoice.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Received invoice not found.");

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.ReceivedInvoiceAttachments.Where(x => x.ReceivedInvoiceId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoicePayments.Where(x => x.ReceivedInvoiceId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoiceItems.Where(x => x.ReceivedInvoiceId == id).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.ReceivedInvoices.Where(x => x.Id == id).ExecuteDeleteAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        await _auditLogService.LogAsync(
            BusinessAuditActionType.ReceivedInvoiceDeleted,
            nameof(ReceivedInvoice),
            invoice.Id.ToString(),
            invoice.InvoiceNumber,
            null,
            cancellationToken);
    }

    public async Task<ReceivedInvoicePaymentDto> RecordPaymentAsync(Guid id, RecordReceivedInvoicePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.ReceivedInvoices
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Received invoice not found.");

        PaymentVoucher? voucher = null;
        if (request.PaymentVoucherId.HasValue)
        {
            voucher = await _dbContext.PaymentVouchers.FirstOrDefaultAsync(x => x.Id == request.PaymentVoucherId.Value, cancellationToken)
                ?? throw new NotFoundException("Payment voucher not found.");

            if (voucher.LinkedReceivedInvoiceId.HasValue && voucher.LinkedReceivedInvoiceId != invoice.Id)
            {
                throw new AppException("Payment voucher is already linked to a different received invoice.");
            }

            voucher.LinkedReceivedInvoiceId = invoice.Id;
        }

        var amount = Round2(request.Amount);
        if (amount > invoice.BalanceDue)
        {
            throw new AppException("Payment amount cannot exceed the remaining balance.");
        }

        var payment = new ReceivedInvoicePayment
        {
            TenantId = invoice.TenantId,
            ReceivedInvoiceId = invoice.Id,
            PaymentDate = request.PaymentDate,
            Amount = amount,
            Method = request.Method,
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim(),
            PaymentVoucherId = voucher?.Id
        };

        _dbContext.ReceivedInvoicePayments.Add(payment);
        invoice.PaymentMethod = request.Method;
        invoice.ReceiptReference = string.IsNullOrWhiteSpace(invoice.ReceiptReference) ? payment.Reference : invoice.ReceiptReference;
        RecalculatePaymentStatus(invoice, invoice.Payments.Select(x => x.Amount).Append(amount).Sum());

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ReceivedInvoicePaymentRecorded,
            nameof(ReceivedInvoice),
            invoice.Id.ToString(),
            invoice.InvoiceNumber,
            new { payment.Amount, payment.Method, payment.PaymentDate, payment.PaymentVoucherId },
            cancellationToken);

        return new ReceivedInvoicePaymentDto
        {
            Id = payment.Id,
            PaymentDate = payment.PaymentDate,
            Amount = payment.Amount,
            Method = payment.Method,
            Reference = payment.Reference,
            Notes = payment.Notes,
            PaymentVoucherId = payment.PaymentVoucherId,
            PaymentVoucherNumber = voucher?.VoucherNumber
        };
    }

    public async Task<ReceivedInvoiceAttachmentDto> UploadAttachmentAsync(Guid id, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.ReceivedInvoices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Received invoice not found.");

        var attachment = new ReceivedInvoiceAttachment
        {
            TenantId = invoice.TenantId,
            ReceivedInvoiceId = invoice.Id,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = content.LongLength,
            Content = content
        };

        _dbContext.ReceivedInvoiceAttachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ReceivedInvoiceAttachmentUploaded,
            nameof(ReceivedInvoice),
            invoice.Id.ToString(),
            invoice.InvoiceNumber,
            new { attachment.FileName, attachment.SizeBytes, attachment.ContentType },
            cancellationToken);

        return new ReceivedInvoiceAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            UploadedAt = attachment.CreatedAt
        };
    }

    public async Task<ReceivedInvoiceAttachmentFileDto> GetAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await _dbContext.ReceivedInvoiceAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReceivedInvoiceId == id && x.Id == attachmentId, cancellationToken)
            ?? throw new NotFoundException("Received invoice attachment not found.");

        return new ReceivedInvoiceAttachmentFileDto
        {
            FileName = attachment.FileName,
            ContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            Content = attachment.Content
        };
    }

    private IQueryable<ReceivedInvoice> BuildQuery(ReceivedInvoiceListQuery query, DateOnly today)
    {
        var invoices = _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Attachments)
            .Where(x =>
                (query.DateFrom == null || x.InvoiceDate >= query.DateFrom)
                && (query.DateTo == null || x.InvoiceDate <= query.DateTo)
                && (query.SupplierId == null || x.SupplierId == query.SupplierId)
                && (query.ExpenseCategoryId == null || x.ExpenseCategoryId == query.ExpenseCategoryId)
                && (query.PaymentStatus == null || x.PaymentStatus == query.PaymentStatus)
                && (query.ApprovalStatus == null || x.ApprovalStatus == query.ApprovalStatus)
                && (query.IsTaxClaimable == null || x.IsTaxClaimable == query.IsTaxClaimable)
                && (!query.OverdueOnly || (x.DueDate < today && x.BalanceDue > 0)));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            invoices = invoices.Where(x =>
                x.InvoiceNumber.ToLower().Contains(search)
                || x.SupplierName.ToLower().Contains(search)
                || (x.SupplierTin != null && x.SupplierTin.ToLower().Contains(search))
                || (x.Outlet != null && x.Outlet.ToLower().Contains(search)));
        }

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? invoices.OrderBy(x => x.InvoiceDate).ThenBy(x => x.InvoiceNumber)
            : invoices.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.CreatedAt);
    }

    private void ApplyItems(
        ReceivedInvoice invoice,
        IReadOnlyCollection<ReceivedInvoiceItemInputDto> items,
        TenantSettings settings,
        decimal? defaultGstRate)
    {
        var allowTax = settings.IsTaxApplicable;
        var requestedHeaderRate = allowTax ? (defaultGstRate ?? items.FirstOrDefault(x => x.GstRate > 0)?.GstRate ?? 0m) : 0m;

        foreach (var item in items)
        {
            var baseAmount = Round2((item.Qty * item.Rate) - item.DiscountAmount);
            var gstRate = allowTax
                ? (item.GstRate > 0 ? item.GstRate : requestedHeaderRate)
                : 0m;
            var gstAmount = Round2(baseAmount * gstRate);

            invoice.Items.Add(new ReceivedInvoiceItem
            {
                Description = item.Description.Trim(),
                Uom = item.Uom?.Trim(),
                Qty = Round2(item.Qty),
                Rate = Round2(item.Rate),
                DiscountAmount = Round2(item.DiscountAmount),
                LineTotal = baseAmount,
                GstRate = gstRate,
                GstAmount = gstAmount
            });
        }

        Recalculate(invoice);
    }

    private void Recalculate(ReceivedInvoice invoice)
    {
        invoice.Subtotal = Round2(invoice.Items.Sum(x => x.LineTotal));
        invoice.DiscountAmount = Round2(invoice.DiscountAmount);

        var taxableBase = Round2(invoice.Subtotal - invoice.DiscountAmount);
        if (taxableBase < 0)
        {
            throw new AppException("Discount amount cannot exceed subtotal.");
        }

        invoice.GstAmount = Round2(invoice.Items.Sum(x => x.GstAmount));
        invoice.GstRate = ResolveHeaderTaxRate(invoice.Items);
        invoice.TotalAmount = Round2(taxableBase + invoice.GstAmount);
        RecalculatePaymentStatus(invoice, invoice.Payments.Sum(x => x.Amount));
    }

    private void RecalculatePaymentStatus(ReceivedInvoice invoice, decimal totalPaid)
    {
        invoice.BalanceDue = Round2(invoice.TotalAmount - totalPaid);
        if (invoice.BalanceDue < 0)
        {
            invoice.BalanceDue = 0;
        }

        if (invoice.BalanceDue <= 0)
        {
            invoice.PaymentStatus = ReceivedInvoiceStatus.Paid;
            return;
        }

        if (totalPaid > 0)
        {
            invoice.PaymentStatus = invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow.Date)
                ? ReceivedInvoiceStatus.Overdue
                : ReceivedInvoiceStatus.Partial;
            return;
        }

        invoice.PaymentStatus = invoice.DueDate < DateOnly.FromDateTime(DateTime.UtcNow.Date)
            ? ReceivedInvoiceStatus.Overdue
            : ReceivedInvoiceStatus.Unpaid;
    }

    private void ApplyApproval(ReceivedInvoice invoice, ApprovalStatus approvalStatus)
    {
        if (approvalStatus != ApprovalStatus.Approved)
        {
            invoice.ApprovedAt = null;
            invoice.ApprovedByUserId = null;
            return;
        }

        invoice.ApprovedAt = DateTimeOffset.UtcNow;
        invoice.ApprovedByUserId = _currentUserService.GetContext().UserId;
    }

    private async Task EnsureInvoiceNumberUniqueAsync(string invoiceNumber, Guid? currentId, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var exists = await _dbContext.ReceivedInvoices
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.Id != currentId && !x.IsDeleted && x.InvoiceNumber == invoiceNumber, cancellationToken);

        if (exists)
        {
            throw new AppException("Received invoice number already exists.");
        }
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static ReceivedInvoiceDetailDto Map(ReceivedInvoice invoice)
    {
        return new ReceivedInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SupplierId = invoice.SupplierId,
            SupplierName = invoice.SupplierName,
            SupplierTin = invoice.SupplierTin,
            SupplierContactNumber = invoice.SupplierContactNumber,
            SupplierEmail = invoice.SupplierEmail,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            Outlet = invoice.Outlet,
            Description = invoice.Description,
            Notes = invoice.Notes,
            Currency = invoice.Currency,
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            GstRate = invoice.GstRate,
            GstAmount = invoice.GstAmount,
            TotalAmount = invoice.TotalAmount,
            BalanceDue = invoice.BalanceDue,
            PaymentStatus = invoice.PaymentStatus,
            PaymentMethod = invoice.PaymentMethod,
            ReceiptReference = invoice.ReceiptReference,
            SettlementReference = invoice.SettlementReference,
            BankName = invoice.BankName,
            BankAccountDetails = invoice.BankAccountDetails,
            MiraTaxableActivityNumber = invoice.MiraTaxableActivityNumber,
            RevenueCapitalClassification = invoice.RevenueCapitalClassification,
            ExpenseCategoryId = invoice.ExpenseCategoryId,
            ExpenseCategoryName = invoice.ExpenseCategory.Name,
            IsTaxClaimable = invoice.IsTaxClaimable,
            ApprovalStatus = invoice.ApprovalStatus,
            ApprovedByUserId = invoice.ApprovedByUserId,
            ApprovedAt = invoice.ApprovedAt,
            Items = invoice.Items.OrderBy(x => x.CreatedAt).Select(x => new ReceivedInvoiceItemDto
            {
                Id = x.Id,
                Description = x.Description,
                Uom = x.Uom,
                Qty = x.Qty,
                Rate = x.Rate,
                DiscountAmount = x.DiscountAmount,
                LineTotal = x.LineTotal,
                GstRate = x.GstRate,
                GstAmount = x.GstAmount
            }).ToList(),
            Payments = invoice.Payments.OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.CreatedAt).Select(x => new ReceivedInvoicePaymentDto
            {
                Id = x.Id,
                PaymentDate = x.PaymentDate,
                Amount = x.Amount,
                Method = x.Method,
                Reference = x.Reference,
                Notes = x.Notes,
                PaymentVoucherId = x.PaymentVoucherId,
                PaymentVoucherNumber = x.PaymentVoucher != null ? x.PaymentVoucher.VoucherNumber : null
            }).ToList(),
            Attachments = invoice.Attachments.OrderByDescending(x => x.CreatedAt).Select(x => new ReceivedInvoiceAttachmentDto
            {
                Id = x.Id,
                FileName = x.FileName,
                ContentType = x.ContentType,
                SizeBytes = x.SizeBytes,
                UploadedAt = x.CreatedAt
            }).ToList()
        };
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return currency == "USD" ? "USD" : "MVR";
    }

    private static decimal ResolveHeaderTaxRate(IEnumerable<ReceivedInvoiceItem> items)
    {
        var rates = items.Select(x => x.GstRate).Distinct().ToList();
        return rates.Count == 1 ? rates[0] : 0m;
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
