using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class DeliveryNoteService : IDeliveryNoteService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher _passwordHasher;

    public DeliveryNoteService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<DeliveryNoteListItemDto>> GetPagedAsync(DeliveryNoteListQuery query, CancellationToken cancellationToken = default)
    {
        var createdAtRange = MaldivesDatePresetRangeResolver.Resolve(
            query.CreatedDatePreset,
            query.CreatedDateFrom,
            query.CreatedDateTo);

        var deliveryNotes = _dbContext.DeliveryNotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vessel)
            .Include(x => x.Invoice)
            .Include(x => x.Items)
            .Where(x =>
                (query.DateFrom == null || x.Date >= query.DateFrom)
                && (query.DateTo == null || x.Date <= query.DateTo)
                && (query.CustomerId == null || x.CustomerId == query.CustomerId)
                && (query.VesselId == null || x.VesselId == query.VesselId)
                && (string.IsNullOrWhiteSpace(query.Search)
                    || x.DeliveryNoteNo.ToLower().Contains(query.Search.ToLower())
                    || x.Customer.Name.ToLower().Contains(query.Search.ToLower())
                    || x.Items.Any(i => i.Details.ToLower().Contains(query.Search.ToLower()))));

        if (createdAtRange.HasValue)
        {
            var (startUtc, endUtc) = createdAtRange.Value;
            deliveryNotes = deliveryNotes.Where(x => x.CreatedAt >= startUtc && x.CreatedAt <= endUtc);
        }

        deliveryNotes = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? deliveryNotes.OrderBy(x => x.Date).ThenBy(x => x.CreatedAt)
            : deliveryNotes.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt);

        return await deliveryNotes
            .Select(x => new DeliveryNoteListItemDto
            {
                Id = x.Id,
                DeliveryNoteNo = x.DeliveryNoteNo,
                PoNumber = x.PoNumber,
                HasPoAttachment = x.PoAttachmentContent != null && x.PoAttachmentSizeBytes > 0,
                PoAttachmentFileName = x.PoAttachmentFileName,
                Date = x.Date,
                Currency = x.Currency,
                Details = x.Items.OrderBy(i => i.CreatedAt).Select(i => i.Details).FirstOrDefault() ?? string.Empty,
                Qty = x.Items.OrderBy(i => i.CreatedAt).Select(i => i.Qty).FirstOrDefault(),
                Customer = x.Customer.Name,
                Vessel = x.Vessel != null ? x.Vessel.Name : null,
                Rate = x.Items.OrderBy(i => i.CreatedAt).Select(i => i.Rate).FirstOrDefault(),
                Total = x.Items.Sum(i => i.Total),
                InvoiceNo = x.Invoice != null ? x.Invoice.InvoiceNo : null,
                CashPayment = x.Items.Sum(i => i.CashPayment),
                VesselPayment = x.VesselPaymentFee > 0 ? x.VesselPaymentFee : x.Items.Sum(i => i.VesselPayment),
                VesselPaymentInvoiceNumber = x.VesselPaymentInvoiceNumber,
                HasVesselPaymentInvoiceAttachment = x.VesselPaymentInvoiceAttachmentContent != null && x.VesselPaymentInvoiceAttachmentSizeBytes > 0,
                VesselPaymentInvoiceAttachmentFileName = x.VesselPaymentInvoiceAttachmentFileName
            })
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<DeliveryNoteDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vessel)
            .Include(x => x.Invoice)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return note is null ? null : MapDeliveryNote(note);
    }

    public async Task<DeliveryNoteDetailDto> CreateAsync(CreateDeliveryNoteRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        if (request.VesselId.HasValue)
        {
            var vesselExists = await _dbContext.Vessels.AnyAsync(x => x.Id == request.VesselId.Value, cancellationToken);
            if (!vesselExists)
            {
                throw new NotFoundException("Vessel not found.");
            }
        }

        var date = request.Date == default ? DateOnly.FromDateTime(DateTime.UtcNow) : request.Date;
        var number = await _documentNumberService.GenerateAsync(DocumentType.DeliveryNote, date, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);

        var note = new DeliveryNote
        {
            DeliveryNoteNo = number,
            PoNumber = request.PoNumber?.Trim(),
            Date = date,
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            CustomerId = customer.Id,
            VesselId = request.VesselId,
            VesselPaymentFee = Math.Round(request.VesselPaymentFee, 2, MidpointRounding.AwayFromZero),
            VesselPaymentInvoiceNumber = request.VesselPaymentInvoiceNumber?.Trim(),
            Notes = request.Notes?.Trim()
        };

        foreach (var item in request.Items)
        {
            note.Items.Add(new DeliveryNoteItem
            {
                Details = item.Details.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate,
                CashPayment = item.CashPayment,
                VesselPayment = item.VesselPayment
            });
        }

        _dbContext.DeliveryNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(note.Id, cancellationToken))!;
    }

    public async Task<DeliveryNoteDetailDto> UpdateAsync(Guid id, UpdateDeliveryNoteRequest request, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (note.InvoiceId.HasValue)
        {
            throw new AppException("Cannot edit a delivery note already linked to invoice.");
        }

        var customerExists = await _dbContext.Customers.AnyAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
        {
            throw new NotFoundException("Customer not found.");
        }

        if (request.VesselId.HasValue)
        {
            var vesselExists = await _dbContext.Vessels.AnyAsync(x => x.Id == request.VesselId.Value, cancellationToken);
            if (!vesselExists)
            {
                throw new NotFoundException("Vessel not found.");
            }
        }

        note.Date = request.Date == default ? note.Date : request.Date;
        note.PoNumber = request.PoNumber?.Trim();
        note.Currency = NormalizeCurrency(request.Currency, note.Currency);
        note.CustomerId = request.CustomerId;
        note.VesselId = request.VesselId;
        note.VesselPaymentFee = Math.Round(request.VesselPaymentFee, 2, MidpointRounding.AwayFromZero);
        note.VesselPaymentInvoiceNumber = request.VesselPaymentInvoiceNumber?.Trim();
        note.Notes = request.Notes?.Trim();

        if (!HasPoReference(note))
        {
            ClearPoAttachment(note);
        }

        if (!HasVesselPaymentDetails(note))
        {
            ClearVesselPaymentAttachment(note);
        }

        foreach (var item in note.Items.ToList())
        {
            _dbContext.DeliveryNoteItems.Remove(item);
        }

        foreach (var item in request.Items)
        {
            note.Items.Add(new DeliveryNoteItem
            {
                Details = item.Details.Trim(),
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Qty * item.Rate,
                CashPayment = item.CashPayment,
                VesselPayment = item.VesselPayment
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(note.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (note.InvoiceId.HasValue)
        {
            throw new AppException("Cannot delete delivery note linked to invoice.");
        }

        _dbContext.DeliveryNotes.Remove(note);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearAllAsync(string password, CancellationToken cancellationToken = default)
    {
        var tenantId = await ValidateCurrentUserPasswordAsync(password, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.DeliveryNoteItems
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.DeliveryNotes
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CreateInvoiceFromDeliveryNoteResultDto> CreateInvoiceFromDeliveryNoteAsync(Guid id, CreateInvoiceFromDeliveryNoteRequest request, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (note.InvoiceId.HasValue)
        {
            throw new AppException("Invoice is already created for this delivery note.");
        }

        if (note.Items.Any(x => x.CashPayment > 0))
        {
            throw new AppException("This delivery note is marked as cash-paid. Invoice is not required.");
        }

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var issuedDate = request.DateIssued ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = request.DateDue ?? issuedDate.AddDays(settings.DefaultDueDays);
        var invoiceNo = await _documentNumberService.GenerateAsync(DocumentType.Invoice, issuedDate, cancellationToken);

        var invoice = new Invoice
        {
            InvoiceNo = invoiceNo,
            CustomerId = note.CustomerId,
            DeliveryNoteId = note.Id,
            CourierVesselId = note.VesselId,
            PoNumber = note.PoNumber,
            DateIssued = issuedDate,
            DateDue = dueDate,
            Currency = NormalizeCurrency(note.Currency, settings.DefaultCurrency),
            TaxRate = settings.IsTaxApplicable ? request.TaxRate ?? settings.DefaultTaxRate : 0m,
            Notes = request.Notes?.Trim()
        };

        foreach (var item in note.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Description = item.Details,
                Qty = item.Qty,
                Rate = item.Rate,
                Total = item.Total
            });
        }

        RecalculateInvoice(invoice);

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(cancellationToken);

        note.InvoiceId = invoice.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateInvoiceFromDeliveryNoteResultDto
        {
            InvoiceId = invoice.Id,
            InvoiceNo = invoice.InvoiceNo
        };
    }

    public async Task<DeliveryNoteAttachmentDto> UploadVesselPaymentInvoiceAttachmentAsync(
        Guid id,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (!HasVesselPaymentDetails(note))
        {
            throw new AppException("Record vessel payment details before uploading the invoice attachment.");
        }

        note.VesselPaymentInvoiceAttachmentFileName = fileName.Trim();
        note.VesselPaymentInvoiceAttachmentContentType = contentType.Trim();
        note.VesselPaymentInvoiceAttachmentSizeBytes = content.LongLength;
        note.VesselPaymentInvoiceAttachmentContent = content;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeliveryNoteAttachmentDto
        {
            FileName = note.VesselPaymentInvoiceAttachmentFileName,
            ContentType = note.VesselPaymentInvoiceAttachmentContentType,
            SizeBytes = note.VesselPaymentInvoiceAttachmentSizeBytes ?? content.LongLength
        };
    }

    public async Task<DeliveryNoteAttachmentDto> UploadPoAttachmentAsync(
        Guid id,
        string fileName,
        string contentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (!HasPoReference(note))
        {
            throw new AppException("Enter a PO number before uploading the PO attachment.");
        }

        note.PoAttachmentFileName = fileName.Trim();
        note.PoAttachmentContentType = contentType.Trim();
        note.PoAttachmentSizeBytes = content.LongLength;
        note.PoAttachmentContent = content;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeliveryNoteAttachmentDto
        {
            FileName = note.PoAttachmentFileName,
            ContentType = note.PoAttachmentContentType,
            SizeBytes = note.PoAttachmentSizeBytes ?? content.LongLength
        };
    }

    public async Task<DeliveryNoteAttachmentFileDto> GetPoAttachmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PoAttachmentFileName,
                x.PoAttachmentContentType,
                x.PoAttachmentSizeBytes,
                x.PoAttachmentContent
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (note.PoAttachmentContent is null
            || note.PoAttachmentContent.Length == 0
            || string.IsNullOrWhiteSpace(note.PoAttachmentFileName))
        {
            throw new NotFoundException("PO attachment not found.");
        }

        return new DeliveryNoteAttachmentFileDto
        {
            FileName = note.PoAttachmentFileName,
            ContentType = string.IsNullOrWhiteSpace(note.PoAttachmentContentType)
                ? "application/octet-stream"
                : note.PoAttachmentContentType,
            SizeBytes = note.PoAttachmentSizeBytes ?? note.PoAttachmentContent.LongLength,
            Content = note.PoAttachmentContent
        };
    }

    public async Task<DeliveryNoteAttachmentFileDto> GetVesselPaymentInvoiceAttachmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await _dbContext.DeliveryNotes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.VesselPaymentInvoiceAttachmentFileName,
                x.VesselPaymentInvoiceAttachmentContentType,
                x.VesselPaymentInvoiceAttachmentSizeBytes,
                x.VesselPaymentInvoiceAttachmentContent
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        if (note.VesselPaymentInvoiceAttachmentContent is null
            || note.VesselPaymentInvoiceAttachmentContent.Length == 0
            || string.IsNullOrWhiteSpace(note.VesselPaymentInvoiceAttachmentFileName))
        {
            throw new NotFoundException("Vessel payment invoice attachment not found.");
        }

        return new DeliveryNoteAttachmentFileDto
        {
            FileName = note.VesselPaymentInvoiceAttachmentFileName,
            ContentType = string.IsNullOrWhiteSpace(note.VesselPaymentInvoiceAttachmentContentType)
                ? "application/octet-stream"
                : note.VesselPaymentInvoiceAttachmentContentType,
            SizeBytes = note.VesselPaymentInvoiceAttachmentSizeBytes ?? note.VesselPaymentInvoiceAttachmentContent.LongLength,
            Content = note.VesselPaymentInvoiceAttachmentContent
        };
    }

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = settings.BuildCompanyInfo();
        return _pdfExportService.BuildDeliveryNotePdf(note, settings.CompanyName, companyInfo, settings.LogoUrl);
    }

    private static DeliveryNoteDetailDto MapDeliveryNote(DeliveryNote note)
    {
        var vesselPaymentTotal = note.VesselPaymentFee > 0
            ? note.VesselPaymentFee
            : note.Items.Sum(x => x.VesselPayment);

        return new DeliveryNoteDetailDto
        {
            Id = note.Id,
            DeliveryNoteNo = note.DeliveryNoteNo,
            PoNumber = note.PoNumber,
            HasPoAttachment = note.PoAttachmentContent is { Length: > 0 },
            PoAttachmentFileName = note.PoAttachmentFileName,
            PoAttachmentContentType = note.PoAttachmentContentType,
            PoAttachmentSizeBytes = note.PoAttachmentSizeBytes,
            Date = note.Date,
            Currency = note.Currency,
            CustomerId = note.CustomerId,
            CustomerName = note.Customer.Name,
            VesselId = note.VesselId,
            VesselName = note.Vessel?.Name,
            Notes = note.Notes,
            InvoiceId = note.InvoiceId,
            InvoiceNo = note.Invoice?.InvoiceNo,
            VesselPaymentFee = vesselPaymentTotal,
            VesselPaymentInvoiceNumber = note.VesselPaymentInvoiceNumber,
            HasVesselPaymentInvoiceAttachment = note.VesselPaymentInvoiceAttachmentContent is { Length: > 0 },
            VesselPaymentInvoiceAttachmentFileName = note.VesselPaymentInvoiceAttachmentFileName,
            VesselPaymentInvoiceAttachmentContentType = note.VesselPaymentInvoiceAttachmentContentType,
            VesselPaymentInvoiceAttachmentSizeBytes = note.VesselPaymentInvoiceAttachmentSizeBytes,
            TotalAmount = note.Items.Sum(x => x.Total),
            Items = note.Items.OrderBy(x => x.CreatedAt).Select(x => new DeliveryNoteItemDto
            {
                Id = x.Id,
                Details = x.Details,
                Qty = x.Qty,
                Rate = x.Rate,
                Total = x.Total,
                CashPayment = x.CashPayment,
                VesselPayment = x.VesselPayment
            }).ToList()
        };
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

    private static bool HasVesselPaymentDetails(DeliveryNote note)
    {
        return note.VesselPaymentFee > 0m || !string.IsNullOrWhiteSpace(note.VesselPaymentInvoiceNumber);
    }

    private static bool HasPoReference(DeliveryNote note)
    {
        return !string.IsNullOrWhiteSpace(note.PoNumber);
    }

    private static void ClearPoAttachment(DeliveryNote note)
    {
        note.PoAttachmentFileName = null;
        note.PoAttachmentContentType = null;
        note.PoAttachmentSizeBytes = null;
        note.PoAttachmentContent = null;
    }

    private static void ClearVesselPaymentAttachment(DeliveryNote note)
    {
        note.VesselPaymentInvoiceAttachmentFileName = null;
        note.VesselPaymentInvoiceAttachmentContentType = null;
        note.VesselPaymentInvoiceAttachmentSizeBytes = null;
        note.VesselPaymentInvoiceAttachmentContent = null;
    }
}
