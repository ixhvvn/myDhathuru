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
                VesselPayment = x.Items.Sum(i => i.VesselPayment)
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
        note.Notes = request.Notes?.Trim();

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
            TaxRate = request.TaxRate ?? settings.DefaultTaxRate,
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

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Delivery note not found.");

        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";
        return _pdfExportService.BuildDeliveryNotePdf(note, settings.CompanyName, companyInfo);
    }

    private static DeliveryNoteDetailDto MapDeliveryNote(DeliveryNote note)
    {
        return new DeliveryNoteDetailDto
        {
            Id = note.Id,
            DeliveryNoteNo = note.DeliveryNoteNo,
            PoNumber = note.PoNumber,
            Date = note.Date,
            Currency = note.Currency,
            CustomerId = note.CustomerId,
            CustomerName = note.Customer.Name,
            VesselId = note.VesselId,
            VesselName = note.Vessel?.Name,
            Notes = note.Notes,
            InvoiceId = note.InvoiceId,
            InvoiceNo = note.Invoice?.InvoiceNo,
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
}
