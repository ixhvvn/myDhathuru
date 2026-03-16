using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Helpers;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly INotificationService _notificationService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public InvoiceService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        INotificationService notificationService,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _notificationService = notificationService;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<InvoiceListItemDto>> GetPagedAsync(InvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        var createdAtRange = MaldivesDatePresetRangeResolver.Resolve(
            query.CreatedDatePreset,
            query.CreatedDateFrom,
            query.CreatedDateTo);

        var invoices = _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.CourierVessel)
            .Where(x =>
                (query.DateFrom == null || x.DateIssued >= query.DateFrom)
                && (query.DateTo == null || x.DateIssued <= query.DateTo)
                && (query.CustomerId == null || x.CustomerId == query.CustomerId)
                && (query.PaymentStatus == null || x.PaymentStatus == query.PaymentStatus)
                && (string.IsNullOrWhiteSpace(query.Search)
                    || x.InvoiceNo.ToLower().Contains(query.Search.ToLower())
                    || (x.Quotation != null && x.Quotation.QuotationNo.ToLower().Contains(query.Search.ToLower()))
                    || x.Customer.Name.ToLower().Contains(query.Search.ToLower())));

        if (createdAtRange.HasValue)
        {
            var (startUtc, endUtc) = createdAtRange.Value;
            invoices = invoices.Where(x => x.CreatedAt >= startUtc && x.CreatedAt <= endUtc);
        }

        invoices = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? invoices.OrderBy(x => x.DateIssued)
            : invoices.OrderByDescending(x => x.DateIssued).ThenByDescending(x => x.CreatedAt);

        return await invoices.Select(x => new InvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNo = x.InvoiceNo,
                QuotationId = x.QuotationId,
                QuotationNo = x.Quotation != null ? x.Quotation.QuotationNo : null,
                Customer = x.Customer.Name,
                CourierId = x.CourierVesselId,
                CourierName = x.CourierVessel != null ? x.CourierVessel.Name : null,
                Currency = x.Currency,
                Amount = x.GrandTotal,
                DateIssued = x.DateIssued,
                DateDue = x.DateDue,
                PaymentStatus = x.PaymentStatus,
                EmailStatus = x.EmailStatus,
                LastEmailedAt = x.LastEmailedAt
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<InvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Quotation)
            .Include(x => x.DeliveryNote)
            .Include(x => x.CourierVessel)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return invoice is null ? null : MapInvoice(invoice);
    }

    public async Task<InvoiceDetailDto> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        Vessel? requestedCourier = null;
        if (request.CourierId.HasValue)
        {
            requestedCourier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        DeliveryNote? deliveryNote = null;
        if (request.DeliveryNoteId.HasValue)
        {
            deliveryNote = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == request.DeliveryNoteId.Value, cancellationToken)
                ?? throw new NotFoundException("Delivery note not found.");

            if (deliveryNote.InvoiceId.HasValue)
            {
                throw new AppException("Delivery note already has an invoice.");
            }

            if (deliveryNote.CustomerId != request.CustomerId)
            {
                throw new AppException("Customer must match the selected delivery note customer.");
            }
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var dateIssued = request.DateIssued;
        var dueDate = request.DateDue ?? dateIssued.AddDays(settings.DefaultDueDays);
        var invoiceNo = await _documentNumberService.GenerateAsync(DocumentType.Invoice, dateIssued, cancellationToken);
        var requestedCurrency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        var invoiceCurrency = requestedCurrency;

        if (deliveryNote is not null)
        {
            var deliveryCurrency = NormalizeCurrency(deliveryNote.Currency, settings.DefaultCurrency);
            if (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(requestedCurrency, deliveryCurrency, StringComparison.Ordinal))
            {
                throw new AppException("Invoice currency must match the selected delivery note currency.");
            }

            invoiceCurrency = deliveryCurrency;
        }

        Guid? courierVesselId = requestedCourier?.Id;
        if (deliveryNote is not null)
        {
            if (request.CourierId.HasValue && request.CourierId != deliveryNote.VesselId)
            {
                throw new AppException("Courier must match the selected delivery note.");
            }

            courierVesselId = deliveryNote.VesselId;
        }

        var invoice = new Invoice
        {
            InvoiceNo = invoiceNo,
            CustomerId = customer.Id,
            DeliveryNoteId = deliveryNote?.Id,
            CourierVesselId = courierVesselId,
            PoNumber = request.PoNumber?.Trim(),
            DateIssued = dateIssued,
            DateDue = dueDate,
            Currency = invoiceCurrency,
            TaxRate = ResolveTaxRate(request.TaxRate, settings),
            Notes = request.Notes?.Trim()
        };

        foreach (var item in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate
            });
        }

        RecalculateInvoice(invoice);

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (deliveryNote is not null)
        {
            deliveryNote.InvoiceId = invoice.Id;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return (await GetByIdAsync(invoice.Id, cancellationToken))!;
    }

    public async Task<InvoiceDetailDto> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        Vessel? requestedCourier = null;
        if (request.CourierId.HasValue)
        {
            requestedCourier = await _dbContext.Vessels.FirstOrDefaultAsync(x => x.Id == request.CourierId.Value, cancellationToken)
                ?? throw new NotFoundException("Courier not found.");
        }

        DeliveryNote? linkedDeliveryNote = null;
        if (invoice.DeliveryNoteId.HasValue)
        {
            if (request.DeliveryNoteId != invoice.DeliveryNoteId)
            {
                throw new AppException("Linked delivery note cannot be changed for this invoice.");
            }

            linkedDeliveryNote = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == invoice.DeliveryNoteId.Value, cancellationToken)
                ?? throw new NotFoundException("Linked delivery note not found.");
        }
        else if (request.DeliveryNoteId.HasValue)
        {
            linkedDeliveryNote = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == request.DeliveryNoteId.Value, cancellationToken)
                ?? throw new NotFoundException("Delivery note not found.");

            if (linkedDeliveryNote.InvoiceId.HasValue && linkedDeliveryNote.InvoiceId != invoice.Id)
            {
                throw new AppException("Delivery note already has an invoice.");
            }

            invoice.DeliveryNoteId = linkedDeliveryNote.Id;
            linkedDeliveryNote.InvoiceId = invoice.Id;
        }

        if (linkedDeliveryNote is not null && linkedDeliveryNote.CustomerId != request.CustomerId)
        {
            throw new AppException("Customer must match the linked delivery note customer.");
        }

        invoice.CustomerId = customer.Id;
        invoice.DateIssued = request.DateIssued;
        invoice.PoNumber = request.PoNumber?.Trim();
        var settings = await GetTenantSettingsAsync(cancellationToken);
        invoice.DateDue = request.DateDue ?? request.DateIssued.AddDays(settings.DefaultDueDays);

        var requestedCurrency = NormalizeCurrency(request.Currency, invoice.Currency);
        if (linkedDeliveryNote is not null)
        {
            var deliveryCurrency = NormalizeCurrency(linkedDeliveryNote.Currency, settings.DefaultCurrency);
            if (!string.IsNullOrWhiteSpace(request.Currency) && !string.Equals(requestedCurrency, deliveryCurrency, StringComparison.Ordinal))
            {
                throw new AppException("Invoice currency must match the linked delivery note currency.");
            }

            requestedCurrency = deliveryCurrency;
        }

        if (invoice.Payments.Any() && !string.Equals(invoice.Currency, requestedCurrency, StringComparison.Ordinal))
        {
            throw new AppException("Invoice currency cannot be changed after receiving payments.");
        }

        Guid? courierVesselId = requestedCourier?.Id;
        if (linkedDeliveryNote is not null)
        {
            if (request.CourierId.HasValue && request.CourierId != linkedDeliveryNote.VesselId)
            {
                throw new AppException("Courier must match the linked delivery note.");
            }

            courierVesselId = linkedDeliveryNote.VesselId;
        }

        invoice.CourierVesselId = courierVesselId;
        invoice.Currency = requestedCurrency;
        invoice.TaxRate = ResolveTaxRate(request.TaxRate, settings);
        invoice.Notes = request.Notes?.Trim();
        ResetEmailStatus(invoice);

        foreach (var item in invoice.Items.ToList())
        {
            _dbContext.InvoiceItems.Remove(item);
        }

        foreach (var item in request.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Description.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate
            });
        }

        RecalculateInvoice(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(invoice.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.DeliveryNoteId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (invoice.DeliveryNoteId.HasValue)
        {
            await _dbContext.DeliveryNotes
                .Where(x => x.Id == invoice.DeliveryNoteId.Value)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.InvoiceId, x => (Guid?)null),
                    cancellationToken);
        }

        await _dbContext.InvoicePayments
            .Where(x => x.InvoiceId == invoice.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.InvoiceItems
            .Where(x => x.InvoiceId == invoice.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Invoices
            .Where(x => x.Id == invoice.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InvoicePaymentDto> ReceivePaymentAsync(Guid invoiceId, ReceiveInvoicePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        if (request.Amount <= 0)
        {
            throw new AppException("Payment amount should be greater than zero.");
        }

        var currentBalance = Math.Max(invoice.Balance, 0);
        var invoiceCurrency = NormalizeCurrency(invoice.Currency, "MVR");
        if (currentBalance <= 0)
        {
            throw new AppException("Invoice is already fully paid.");
        }

        if (request.Amount > currentBalance)
        {
            throw new AppException($"Payment cannot exceed current balance ({invoiceCurrency} {currentBalance:0.00}).");
        }

        var paymentCurrency = NormalizeCurrency(request.Currency, invoiceCurrency);
        if (!string.Equals(invoiceCurrency, paymentCurrency, StringComparison.Ordinal))
        {
            throw new AppException("Payment currency must match invoice currency.");
        }

        var payment = new InvoicePayment
        {
            InvoiceId = invoice.Id,
            Currency = paymentCurrency,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : request.PaymentDate,
            Method = request.Method,
            Reference = request.Reference?.Trim(),
            Notes = request.Notes?.Trim()
        };

        // Insert payment explicitly to avoid change-tracker reclassifying the entity as update.
        _dbContext.InvoicePayments.Add(payment);

        invoice.AmountPaid = Math.Round(invoice.AmountPaid + request.Amount, 2, MidpointRounding.AwayFromZero);
        invoice.Balance = Math.Round(invoice.GrandTotal - invoice.AmountPaid, 2, MidpointRounding.AwayFromZero);

        if (invoice.Balance <= 0)
        {
            invoice.Balance = 0;
            invoice.PaymentStatus = PaymentStatus.Paid;
        }
        else if (invoice.AmountPaid > 0)
        {
            invoice.PaymentStatus = PaymentStatus.Partial;
        }
        else
        {
            invoice.PaymentStatus = PaymentStatus.Unpaid;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InvoicePaymentDto
        {
            Id = payment.Id,
            Currency = payment.Currency,
            Amount = payment.Amount,
            PaymentDate = payment.PaymentDate,
            Method = payment.Method,
            Reference = payment.Reference,
            Notes = payment.Notes
        };
    }

    public async Task ClearAllAsync(string password, CancellationToken cancellationToken = default)
    {
        var tenantId = await ValidateCurrentUserPasswordAsync(password, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.DeliveryNotes
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.InvoiceId != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.InvoiceId, x => (Guid?)null), cancellationToken);

        await _dbContext.InvoicePayments
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.InvoiceItems
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Invoices
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InvoicePaymentDto>> GetPaymentHistoryAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Invoices.AnyAsync(x => x.Id == invoiceId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Invoice not found.");
        }

        return await _dbContext.InvoicePayments
            .AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId)
            .OrderByDescending(x => x.PaymentDate)
            .Select(x => new InvoicePaymentDto
            {
                Id = x.Id,
                Currency = x.Currency,
                Amount = x.Amount,
                PaymentDate = x.PaymentDate,
                Method = x.Method,
                Reference = x.Reference,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SendEmailAsync(Guid id, SendInvoiceEmailRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .Include(x => x.Customer)
            .Include(x => x.Quotation)
            .Include(x => x.DeliveryNote)
            .Include(x => x.CourierVessel)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        var recipientEmail = invoice.Customer.Email?.Trim();
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            throw new AppException("Customer email is required before sending this invoice.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var body = DocumentEmailTemplateHelper.RenderInvoice(
            string.IsNullOrWhiteSpace(request.Body) ? settings.InvoiceEmailBodyTemplate : request.Body,
            settings.CompanyName);
        var ccEmail = string.IsNullOrWhiteSpace(request.CcEmail) ? null : request.CcEmail.Trim();
        var subject = $"Invoice {invoice.InvoiceNo} from {settings.CompanyName}";
        var pdfBytes = BuildInvoicePdf(MapInvoice(invoice), settings);

        await _notificationService.SendDocumentEmailAsync(
            recipientEmail,
            ccEmail,
            subject,
            body,
            pdfBytes,
            $"{invoice.InvoiceNo}.pdf",
            settings.CompanyName,
            settings.CompanyEmail,
            cancellationToken);

        invoice.EmailStatus = DocumentEmailStatus.Emailed;
        invoice.LastEmailedAt = DateTimeOffset.UtcNow;
        invoice.LastEmailedTo = recipientEmail;
        invoice.LastEmailedCc = ccEmail;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Invoice not found.");

        var settings = await GetTenantSettingsAsync(cancellationToken);
        return BuildInvoicePdf(invoice, settings);
    }

    private static void RecalculateInvoice(Invoice invoice)
    {
        invoice.Subtotal = Math.Round(invoice.Items.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero);
        invoice.TaxAmount = Math.Round(invoice.Subtotal * invoice.TaxRate, 2, MidpointRounding.AwayFromZero);
        invoice.GrandTotal = Math.Round(invoice.Subtotal + invoice.TaxAmount, 2, MidpointRounding.AwayFromZero);
        invoice.AmountPaid = Math.Round(invoice.Payments.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        invoice.Balance = Math.Round(invoice.GrandTotal - invoice.AmountPaid, 2, MidpointRounding.AwayFromZero);

        invoice.PaymentStatus = invoice.Balance switch
        {
            <= 0 => PaymentStatus.Paid,
            _ when invoice.AmountPaid > 0 => PaymentStatus.Partial,
            _ => PaymentStatus.Unpaid
        };
    }

    private static decimal ResolveTaxRate(decimal? requestedTaxRate, TenantSettings settings)
    {
        if (!settings.IsTaxApplicable)
        {
            return 0m;
        }

        return requestedTaxRate ?? settings.DefaultTaxRate;
    }

    private static InvoiceDetailDto MapInvoice(Invoice invoice)
    {
        return new InvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            QuotationId = invoice.QuotationId,
            QuotationNo = invoice.Quotation?.QuotationNo,
            PoNumber = invoice.PoNumber,
            CustomerId = invoice.CustomerId,
            CustomerName = invoice.Customer.Name,
            CustomerTinNumber = invoice.Customer.TinNumber,
            CustomerEmail = invoice.Customer.Email,
            CustomerPhone = invoice.Customer.Phone,
            DeliveryNoteId = invoice.DeliveryNoteId,
            DeliveryNoteNo = invoice.DeliveryNote?.DeliveryNoteNo,
            CourierId = invoice.CourierVesselId,
            CourierName = invoice.CourierVessel?.Name,
            DateIssued = invoice.DateIssued,
            DateDue = invoice.DateDue,
            Currency = invoice.Currency,
            Subtotal = invoice.Subtotal,
            TaxRate = invoice.TaxRate,
            TaxAmount = invoice.TaxAmount,
            GrandTotal = invoice.GrandTotal,
            AmountPaid = invoice.AmountPaid,
            Balance = invoice.Balance,
            PaymentStatus = invoice.PaymentStatus,
            EmailStatus = invoice.EmailStatus,
            LastEmailedAt = invoice.LastEmailedAt,
            Notes = invoice.Notes,
            Items = invoice.Items.OrderBy(x => x.CreatedAt).Select(item => new InvoiceItemDto
            {
                Id = item.Id,
                Description = item.Description,
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Total
            }).ToList(),
            Payments = invoice.Payments.OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.CreatedAt).Select(payment => new InvoicePaymentDto
            {
                Id = payment.Id,
                Currency = payment.Currency,
                Amount = payment.Amount,
                PaymentDate = payment.PaymentDate,
                Method = payment.Method,
                Reference = payment.Reference,
                Notes = payment.Notes
            }).ToList()
        };
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private async Task<Guid> ValidateCurrentUserPasswordAsync(string password, CancellationToken cancellationToken)
    {
        var context = _currentUserService.GetContext();
        if (!context.UserId.HasValue || !context.TenantId.HasValue)
        {
            throw new UnauthorizedException("Unauthorized.");
        }

        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.Id == context.UserId.Value && x.TenantId == context.TenantId.Value && !x.IsDeleted,
                cancellationToken)
            ?? throw new UnauthorizedException("User not found.");

        if (!_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            throw new AppException("Invalid password.");
        }

        return context.TenantId.Value;
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

    private static void ResetEmailStatus(Invoice invoice)
    {
        invoice.EmailStatus = DocumentEmailStatus.Pending;
        invoice.LastEmailedAt = null;
        invoice.LastEmailedTo = null;
        invoice.LastEmailedCc = null;
    }

    private byte[] BuildInvoicePdf(InvoiceDetailDto invoice, TenantSettings settings)
    {
        var companyInfo = settings.BuildCompanyInfo(includeBusinessRegistration: true);
        var bankDetails = new InvoiceBankDetailsDto
        {
            BmlMvrAccountName = settings.BmlMvrAccountName,
            BmlMvrAccountNumber = settings.BmlMvrAccountNumber,
            BmlUsdAccountName = settings.BmlUsdAccountName,
            BmlUsdAccountNumber = settings.BmlUsdAccountNumber,
            MibMvrAccountName = settings.MibMvrAccountName,
            MibMvrAccountNumber = settings.MibMvrAccountNumber,
            MibUsdAccountName = settings.MibUsdAccountName,
            MibUsdAccountNumber = settings.MibUsdAccountNumber,
            InvoiceOwnerName = settings.InvoiceOwnerName,
            InvoiceOwnerIdCard = settings.InvoiceOwnerIdCard
        };

        return _pdfExportService.BuildInvoicePdf(invoice, settings.CompanyName, companyInfo, bankDetails, settings.LogoUrl, settings.IsTaxApplicable);
    }
}
