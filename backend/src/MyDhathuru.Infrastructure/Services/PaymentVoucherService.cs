using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PaymentVouchers.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class PaymentVoucherService : IPaymentVoucherService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;

    public PaymentVoucherService(
        ApplicationDbContext dbContext,
        IDocumentNumberService documentNumberService,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService,
        IBusinessAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _documentNumberService = documentNumberService;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<PaymentVoucherListItemDto>> GetPagedAsync(PaymentVoucherListQuery query, CancellationToken cancellationToken = default)
    {
        var vouchers = BuildQuery(query).Select(x => new PaymentVoucherListItemDto
        {
            Id = x.Id,
            VoucherNumber = x.VoucherNumber,
            Date = x.Date,
            PayTo = x.PayTo,
            PaymentMethod = x.PaymentMethod,
            Amount = x.Amount,
            Status = x.Status,
            Bank = x.Bank,
            LinkedReceivedInvoiceNumber = x.LinkedReceivedInvoice != null ? x.LinkedReceivedInvoice.InvoiceNumber : null
        });

        return await vouchers.ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<PaymentVoucherDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers
            .AsNoTracking()
            .Include(x => x.LinkedReceivedInvoice)
            .Include(x => x.LinkedExpenseEntry)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return voucher is null ? null : Map(voucher);
    }

    public async Task<PaymentVoucherDetailDto> CreateAsync(CreatePaymentVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var receivedInvoice = await ResolveReceivedInvoiceAsync(request.LinkedReceivedInvoiceId, cancellationToken);
        var linkedExpense = await ResolveExpenseEntryAsync(request.LinkedExpenseEntryId, cancellationToken);

        var voucher = new PaymentVoucher
        {
            VoucherNumber = await _documentNumberService.GenerateAsync(DocumentType.PaymentVoucher, request.Date, cancellationToken),
            Date = request.Date,
            PayTo = request.PayTo.Trim(),
            Details = request.Details.Trim(),
            PaymentMethod = request.PaymentMethod,
            AccountNumber = request.AccountNumber?.Trim(),
            ChequeNumber = request.ChequeNumber?.Trim(),
            Bank = request.Bank?.Trim(),
            Amount = Round2(request.Amount),
            AmountInWords = string.IsNullOrWhiteSpace(request.AmountInWords)
                ? NumberToWordsConverter.ToMoneyWords(request.Amount)
                : request.AmountInWords.Trim(),
            ApprovedBy = request.ApprovedBy?.Trim(),
            ReceivedBy = request.ReceivedBy?.Trim(),
            LinkedReceivedInvoiceId = receivedInvoice?.Id,
            LinkedExpenseEntryId = linkedExpense?.Id,
            Notes = request.Notes?.Trim(),
            Status = request.Status
        };

        await ApplyStatusMetadataAsync(voucher, request.Status, cancellationToken);

        _dbContext.PaymentVouchers.Add(voucher);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.PaymentVoucherCreated,
            nameof(PaymentVoucher),
            voucher.Id.ToString(),
            voucher.VoucherNumber,
            new { voucher.PayTo, voucher.Amount, voucher.Status },
            cancellationToken);

        return (await GetByIdAsync(voucher.Id, cancellationToken))!;
    }

    public async Task<PaymentVoucherDetailDto> UpdateAsync(Guid id, UpdatePaymentVoucherRequest request, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payment voucher not found.");

        var receivedInvoice = await ResolveReceivedInvoiceAsync(request.LinkedReceivedInvoiceId, cancellationToken);
        var linkedExpense = await ResolveExpenseEntryAsync(request.LinkedExpenseEntryId, cancellationToken);

        voucher.Date = request.Date;
        voucher.PayTo = request.PayTo.Trim();
        voucher.Details = request.Details.Trim();
        voucher.PaymentMethod = request.PaymentMethod;
        voucher.AccountNumber = request.AccountNumber?.Trim();
        voucher.ChequeNumber = request.ChequeNumber?.Trim();
        voucher.Bank = request.Bank?.Trim();
        voucher.Amount = Round2(request.Amount);
        voucher.AmountInWords = string.IsNullOrWhiteSpace(request.AmountInWords)
            ? NumberToWordsConverter.ToMoneyWords(request.Amount)
            : request.AmountInWords.Trim();
        voucher.ApprovedBy = request.ApprovedBy?.Trim();
        voucher.ReceivedBy = request.ReceivedBy?.Trim();
        voucher.LinkedReceivedInvoiceId = receivedInvoice?.Id;
        voucher.LinkedExpenseEntryId = linkedExpense?.Id;
        voucher.Notes = request.Notes?.Trim();
        voucher.Status = request.Status;

        await ApplyStatusMetadataAsync(voucher, request.Status, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.PaymentVoucherUpdated,
            nameof(PaymentVoucher),
            voucher.Id.ToString(),
            voucher.VoucherNumber,
            new { voucher.PayTo, voucher.Amount, voucher.Status },
            cancellationToken);

        return (await GetByIdAsync(voucher.Id, cancellationToken))!;
    }

    public async Task<PaymentVoucherDetailDto> UpdateStatusAsync(Guid id, PaymentVoucherStatus status, string? notes, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payment voucher not found.");

        voucher.Status = status;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            voucher.Notes = notes.Trim();
        }

        await ApplyStatusMetadataAsync(voucher, status, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var actionType = status switch
        {
            PaymentVoucherStatus.Approved => BusinessAuditActionType.PaymentVoucherApproved,
            PaymentVoucherStatus.Posted => BusinessAuditActionType.PaymentVoucherPosted,
            PaymentVoucherStatus.Cancelled => BusinessAuditActionType.PaymentVoucherCancelled,
            _ => BusinessAuditActionType.PaymentVoucherUpdated
        };

        await _auditLogService.LogAsync(
            actionType,
            nameof(PaymentVoucher),
            voucher.Id.ToString(),
            voucher.VoucherNumber,
            new { voucher.Status, voucher.Amount, voucher.PayTo },
            cancellationToken);

        return (await GetByIdAsync(voucher.Id, cancellationToken))!;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Payment voucher not found.");

        var hasPayments = await _dbContext.ReceivedInvoicePayments.AnyAsync(x => x.PaymentVoucherId == id, cancellationToken);
        if (hasPayments)
        {
            throw new AppException("Payment voucher is already linked to supplier payments and cannot be deleted.");
        }

        _dbContext.PaymentVouchers.Remove(voucher);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.PaymentVoucherDeleted,
            nameof(PaymentVoucher),
            voucher.Id.ToString(),
            voucher.VoucherNumber,
            null,
            cancellationToken);
    }

    public async Task<byte[]> GeneratePdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voucher = await GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Payment voucher not found.");
        var settings = await GetTenantSettingsAsync(cancellationToken);
        return _pdfExportService.BuildPaymentVoucherPdf(voucher, settings.CompanyName, settings.BuildCompanyInfo(includeBusinessRegistration: true), settings.LogoUrl);
    }

    private IQueryable<PaymentVoucher> BuildQuery(PaymentVoucherListQuery query)
    {
        var vouchers = _dbContext.PaymentVouchers
            .AsNoTracking()
            .Include(x => x.LinkedReceivedInvoice)
            .Include(x => x.LinkedExpenseEntry)
            .Where(x =>
                (query.DateFrom == null || x.Date >= query.DateFrom)
                && (query.DateTo == null || x.Date <= query.DateTo)
                && (query.Status == null || x.Status == query.Status)
                && (query.LinkedReceivedInvoiceId == null || x.LinkedReceivedInvoiceId == query.LinkedReceivedInvoiceId));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            vouchers = vouchers.Where(x =>
                x.VoucherNumber.ToLower().Contains(search)
                || x.PayTo.ToLower().Contains(search)
                || x.Details.ToLower().Contains(search)
                || (x.Bank != null && x.Bank.ToLower().Contains(search)));
        }

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? vouchers.OrderBy(x => x.Date).ThenBy(x => x.VoucherNumber)
            : vouchers.OrderByDescending(x => x.Date).ThenByDescending(x => x.CreatedAt);
    }

    private async Task ApplyStatusMetadataAsync(PaymentVoucher voucher, PaymentVoucherStatus status, CancellationToken cancellationToken)
    {
        var currentUserName = await GetCurrentUserNameAsync(cancellationToken);

        if (status == PaymentVoucherStatus.Approved || status == PaymentVoucherStatus.Posted)
        {
            voucher.ApprovedAt ??= DateTimeOffset.UtcNow;
            if (string.IsNullOrWhiteSpace(voucher.ApprovedBy))
            {
                voucher.ApprovedBy = currentUserName;
            }
        }
        else if (status == PaymentVoucherStatus.Draft)
        {
            voucher.ApprovedAt = null;
            voucher.PostedAt = null;
        }

        if (status == PaymentVoucherStatus.Posted)
        {
            voucher.PostedAt ??= DateTimeOffset.UtcNow;
        }
        else if (status != PaymentVoucherStatus.Cancelled)
        {
            voucher.PostedAt = null;
        }
    }

    private async Task<ReceivedInvoice?> ResolveReceivedInvoiceAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (!id.HasValue)
        {
            return null;
        }

        return await _dbContext.ReceivedInvoices.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
            ?? throw new NotFoundException("Linked received invoice not found.");
    }

    private async Task<ExpenseEntry?> ResolveExpenseEntryAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (!id.HasValue)
        {
            return null;
        }

        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        if (entry is null)
        {
            throw new NotFoundException("Linked expense entry not found.");
        }

        if (entry.SourceType != ExpenseSourceType.Manual)
        {
            throw new AppException("Only manual expense entries can be linked directly to payment vouchers.");
        }

        return entry;
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private async Task<string> GetCurrentUserNameAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetContext().UserId;
        if (!userId.HasValue)
        {
            return "System";
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? "System";
    }

    private static PaymentVoucherDetailDto Map(PaymentVoucher voucher)
    {
        return new PaymentVoucherDetailDto
        {
            Id = voucher.Id,
            VoucherNumber = voucher.VoucherNumber,
            Date = voucher.Date,
            PayTo = voucher.PayTo,
            Details = voucher.Details,
            PaymentMethod = voucher.PaymentMethod,
            AccountNumber = voucher.AccountNumber,
            ChequeNumber = voucher.ChequeNumber,
            Bank = voucher.Bank,
            Amount = voucher.Amount,
            AmountInWords = voucher.AmountInWords,
            ApprovedBy = voucher.ApprovedBy,
            ReceivedBy = voucher.ReceivedBy,
            LinkedReceivedInvoiceId = voucher.LinkedReceivedInvoiceId,
            LinkedReceivedInvoiceNumber = voucher.LinkedReceivedInvoice?.InvoiceNumber,
            LinkedExpenseEntryId = voucher.LinkedExpenseEntryId,
            LinkedExpenseDocumentNumber = voucher.LinkedExpenseEntry?.DocumentNumber,
            Notes = voucher.Notes,
            Status = voucher.Status,
            ApprovedAt = voucher.ApprovedAt,
            PostedAt = voucher.PostedAt
        };
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static class NumberToWordsConverter
    {
        private static readonly string[] Units =
        {
            "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
        };

        private static readonly string[] Tens =
        {
            string.Empty, string.Empty, "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
        };

        public static string ToMoneyWords(decimal amount)
        {
            var rounded = Round2(amount);
            var whole = (long)Math.Floor(rounded);
            var fraction = (int)((rounded - whole) * 100m);
            return $"{ToWords(whole)} and {fraction:00}/100";
        }

        private static string ToWords(long number)
        {
            if (number < 20)
            {
                return Units[number];
            }

            if (number < 100)
            {
                return number % 10 == 0
                    ? Tens[number / 10]
                    : $"{Tens[number / 10]} {Units[number % 10]}";
            }

            if (number < 1000)
            {
                return number % 100 == 0
                    ? $"{Units[number / 100]} Hundred"
                    : $"{Units[number / 100]} Hundred {ToWords(number % 100)}";
            }

            if (number < 1_000_000)
            {
                return number % 1000 == 0
                    ? $"{ToWords(number / 1000)} Thousand"
                    : $"{ToWords(number / 1000)} Thousand {ToWords(number % 1000)}";
            }

            if (number < 1_000_000_000)
            {
                return number % 1_000_000 == 0
                    ? $"{ToWords(number / 1_000_000)} Million"
                    : $"{ToWords(number / 1_000_000)} Million {ToWords(number % 1_000_000)}";
            }

            return number % 1_000_000_000 == 0
                ? $"{ToWords(number / 1_000_000_000)} Billion"
                : $"{ToWords(number / 1_000_000_000)} Billion {ToWords(number % 1_000_000_000)}";
        }
    }
}
