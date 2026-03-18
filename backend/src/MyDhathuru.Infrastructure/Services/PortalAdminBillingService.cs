using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class PortalAdminBillingService : IPortalAdminBillingService
{
    private const decimal DefaultSoftwareFee = 2500m;
    private const decimal DefaultVesselFee = 1000m;
    private const decimal DefaultStaffFee = 250m;
    private const int DefaultDueDays = 14;
    private const string DefaultInvoiceLogoUrl = "/newlogo.png";
    private const int SequenceLockKey = 73190511;

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPdfExportService _pdfExportService;
    private readonly INotificationService _notificationService;

    public PortalAdminBillingService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IPdfExportService pdfExportService,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _pdfExportService = pdfExportService;
        _notificationService = notificationService;
    }

    public async Task<PortalAdminBillingDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var nextMonthStart = monthStart.AddMonths(1);

        var currentMonthInvoicesQuery = _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.CreatedAt >= monthStart && x.CreatedAt < nextMonthStart);

        var groupedByCurrency = await currentMonthInvoicesQuery
            .GroupBy(x => x.Currency == "USD" ? "USD" : "MVR")
            .Select(g => new
            {
                Currency = g.Key,
                Total = g.Sum(x => x.Total)
            })
            .ToListAsync(cancellationToken);

        var totals = BuildCurrencyTotals(groupedByCurrency.Select(x => (x.Currency, x.Total)));
        var totalEmailed = await _dbContext.AdminInvoiceEmailLogs.IgnoreQueryFilters()
            .CountAsync(
                x => !x.IsDeleted && x.AttemptedAt >= monthStart && x.AttemptedAt < nextMonthStart && x.Status == AdminInvoiceEmailStatus.Sent,
                cancellationToken);

        var pendingEmailCount = await currentMonthInvoicesQuery
            .CountAsync(x => x.Status != AdminInvoiceStatus.Emailed, cancellationToken);

        var recentInvoices = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => new PortalAdminBillingInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                BillingMonth = x.BillingMonth,
                InvoiceDate = x.InvoiceDate,
                DueDate = x.DueDate,
                TenantId = x.TenantId,
                CompanyName = x.CompanyNameSnapshot,
                Total = x.Total,
                Currency = x.Currency == "USD" ? "USD" : "MVR",
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SentAt = x.SentAt,
                IsCustom = x.IsCustom
            })
            .ToListAsync(cancellationToken);

        return new PortalAdminBillingDashboardDto
        {
            InvoicesGeneratedThisMonth = await currentMonthInvoicesQuery.CountAsync(cancellationToken),
            TotalBilledMvrThisMonth = totals.Mvr,
            TotalBilledUsdThisMonth = totals.Usd,
            TotalEmailedThisMonth = totalEmailed,
            PendingEmailCount = pendingEmailCount,
            RecentInvoices = recentInvoices
        };
    }

    public async Task<PortalAdminBillingSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<PortalAdminBillingSettingsDto> UpdateSettingsAsync(UpdatePortalAdminBillingSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var settings = await GetOrCreateSettingsAsync(cancellationToken);

        settings.BasicSoftwareFee = RoundMoney(request.BasicSoftwareFee);
        settings.VesselFee = RoundMoney(request.VesselFee);
        settings.StaffFee = RoundMoney(request.StaffFee);
        settings.InvoicePrefix = NormalizePrefix(request.InvoicePrefix);
        settings.StartingSequenceNumber = Math.Max(1, request.StartingSequenceNumber);
        settings.DefaultCurrency = NormalizeCurrency(request.DefaultCurrency, "MVR");
        settings.DefaultDueDays = Math.Clamp(request.DefaultDueDays, 1, 120);
        settings.AccountName = request.AccountName.Trim();
        settings.AccountNumber = request.AccountNumber.Trim();
        settings.BankName = TrimOrNull(request.BankName);
        settings.Branch = TrimOrNull(request.Branch);
        settings.PaymentInstructions = TrimOrNull(request.PaymentInstructions);
        settings.InvoiceFooterNote = TrimOrNull(request.InvoiceFooterNote);
        settings.InvoiceTerms = TrimOrNull(request.InvoiceTerms);
        settings.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? DefaultInvoiceLogoUrl : request.LogoUrl.Trim();
        settings.EmailFromName = TrimOrNull(request.EmailFromName);
        settings.ReplyToEmail = TrimOrNull(request.ReplyToEmail)?.ToLowerInvariant();
        settings.AutoGenerationEnabled = request.AutoGenerationEnabled;
        settings.AutoEmailEnabled = request.AutoEmailEnabled;

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.BillingSettingsUpdated,
            nameof(AdminBillingSettings),
            settings.Id.ToString(),
            "Portal billing settings",
            null,
            new
            {
                settings.BasicSoftwareFee,
                settings.VesselFee,
                settings.StaffFee,
                settings.InvoicePrefix,
                settings.DefaultCurrency,
                settings.DefaultDueDays
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapSettings(settings);
    }

    public async Task<IReadOnlyList<PortalAdminBillingBusinessOptionDto>> GetBusinessOptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var businesses = await GetBillableTenantsQuery()
            .OrderBy(x => x.CompanyName)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.CompanyEmail,
                IsActive = x.IsActive && x.AccountStatus == BusinessAccountStatus.Active
            })
            .ToListAsync(cancellationToken);

        if (businesses.Count == 0)
        {
            return Array.Empty<PortalAdminBillingBusinessOptionDto>();
        }

        var businessIds = businesses.Select(x => x.Id).ToArray();
        var staffCounts = await _dbContext.Staff.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && businessIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var vesselCounts = await _dbContext.Vessels.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && businessIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        return businesses.Select(x => new PortalAdminBillingBusinessOptionDto
        {
            TenantId = x.Id,
            CompanyName = x.CompanyName,
            CompanyEmail = x.CompanyEmail,
            IsActive = x.IsActive,
            StaffCount = staffCounts.GetValueOrDefault(x.Id),
            VesselCount = vesselCounts.GetValueOrDefault(x.Id)
        }).ToList();
    }

    public async Task<PortalAdminBillingYearlyStatementDto> GetYearlyStatementAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        if (year < 2000 || year > 2100)
        {
            throw new AppException("Year must be between 2000 and 2100.");
        }

        var tenant = await GetBillableTenantsQuery()
            .Where(x => x.Id == tenantId)
            .Select(x => new
            {
                x.Id,
                x.CompanyName
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Business not found.");

        var periodStart = new DateOnly(year, 1, 1);
        var periodEnd = periodStart.AddYears(1);
        var invoices = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenantId && x.BillingMonth >= periodStart && x.BillingMonth < periodEnd)
            .OrderBy(x => x.BillingMonth)
            .ThenBy(x => x.InvoiceDate)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new PortalAdminBillingInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                BillingMonth = x.BillingMonth,
                InvoiceDate = x.InvoiceDate,
                DueDate = x.DueDate,
                TenantId = x.TenantId,
                CompanyName = x.CompanyNameSnapshot,
                Total = x.Total,
                Currency = x.Currency == "USD" ? "USD" : "MVR",
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SentAt = x.SentAt,
                IsCustom = x.IsCustom
            })
            .ToListAsync(cancellationToken);

        var totals = BuildCurrencyTotals(invoices.Select(x => (x.Currency, x.Total)));
        var invoicesByMonth = invoices
            .GroupBy(x => x.BillingMonth.Month)
            .ToDictionary(x => x.Key, x => x.ToList());

        var months = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var monthInvoices = invoicesByMonth.GetValueOrDefault(month) ?? new List<PortalAdminBillingInvoiceListItemDto>();
                var monthStart = new DateOnly(year, month, 1);
                var monthTotals = BuildCurrencyTotals(monthInvoices.Select(x => (x.Currency, x.Total)));
                var emailedCount = monthInvoices.Count(x => x.Status == AdminInvoiceStatus.Emailed);

                return new PortalAdminBillingYearlyStatementMonthDto
                {
                    Month = month,
                    Label = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    InvoiceCount = monthInvoices.Count,
                    TotalMvr = monthTotals.Mvr,
                    TotalUsd = monthTotals.Usd,
                    EmailedCount = emailedCount,
                    PendingCount = monthInvoices.Count - emailedCount
                };
            })
            .ToList();

        var emailedInvoices = invoices.Count(x => x.Status == AdminInvoiceStatus.Emailed);
        return new PortalAdminBillingYearlyStatementDto
        {
            TenantId = tenant.Id,
            CompanyName = tenant.CompanyName,
            Year = year,
            TotalInvoices = invoices.Count,
            EmailedInvoices = emailedInvoices,
            PendingInvoices = invoices.Count - emailedInvoices,
            TotalInvoiced = new PortalAdminBillingStatementTotalsDto
            {
                Mvr = totals.Mvr,
                Usd = totals.Usd
            },
            Months = months,
            Invoices = invoices
        };
    }

    public async Task<PagedResult<PortalAdminBillingInvoiceListItemDto>> GetInvoicesAsync(PortalAdminBillingInvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var invoicesQuery = _dbContext.AdminInvoices.IgnoreQueryFilters().Where(x => !x.IsDeleted);

        if (query.TenantId.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(x => x.TenantId == query.TenantId.Value);
        }

        if (query.BillingMonth.HasValue)
        {
            var targetMonth = new DateOnly(query.BillingMonth.Value.Year, query.BillingMonth.Value.Month, 1);
            invoicesQuery = invoicesQuery.Where(x => x.BillingMonth == targetMonth);
        }

        if (query.Status.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(x => x.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var currency = NormalizeCurrency(query.Currency, "MVR");
            invoicesQuery = invoicesQuery.Where(x => x.Currency == currency);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            invoicesQuery = invoicesQuery.Where(x =>
                x.InvoiceNumber.ToLower().Contains(search)
                || x.CompanyNameSnapshot.ToLower().Contains(search)
                || x.CompanyEmailSnapshot.ToLower().Contains(search));
        }

        var totalCount = await invoicesQuery.CountAsync(cancellationToken);
        var items = await invoicesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PortalAdminBillingInvoiceListItemDto
            {
                Id = x.Id,
                InvoiceNumber = x.InvoiceNumber,
                BillingMonth = x.BillingMonth,
                InvoiceDate = x.InvoiceDate,
                DueDate = x.DueDate,
                TenantId = x.TenantId,
                CompanyName = x.CompanyNameSnapshot,
                Total = x.Total,
                Currency = x.Currency == "USD" ? "USD" : "MVR",
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                SentAt = x.SentAt,
                IsCustom = x.IsCustom
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PortalAdminBillingInvoiceListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PortalAdminBillingInvoiceDetailDto> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var invoice = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Include(x => x.LineItems)
            .Include(x => x.EmailLogs)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Billing invoice not found.");

        return MapInvoiceDetail(invoice);
    }

    public async Task<PortalAdminBillingGenerationResultDto> GenerateInvoiceAsync(PortalAdminBillingGenerateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var tenant = await GetBillableTenantByIdAsync(request.TenantId, cancellationToken);

        return await GenerateInvoicesCoreAsync(
            actor,
            new[] { tenant },
            new DateOnly(request.BillingMonth.Year, request.BillingMonth.Month, 1),
            request.AllowDuplicateForMonth,
            request.PreviewOnly,
            isCustom: false,
            customRequest: null,
            AdminAuditActionType.BillingInvoiceGenerated,
            cancellationToken);
    }

    public async Task<PortalAdminBillingGenerationResultDto> GenerateBulkInvoicesAsync(PortalAdminBillingGenerateBulkInvoicesRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var normalizedMonth = new DateOnly(request.BillingMonth.Year, request.BillingMonth.Month, 1);
        var query = GetBillableTenantsQuery();

        if (!request.IncludeDisabledBusinesses)
        {
            query = query.Where(x => x.IsActive && x.AccountStatus == BusinessAccountStatus.Active);
        }

        if (request.TenantIds.Count > 0)
        {
            query = query.Where(x => request.TenantIds.Contains(x.Id));
        }

        var tenants = await query.OrderBy(x => x.CompanyName).ToListAsync(cancellationToken);

        return await GenerateInvoicesCoreAsync(
            actor,
            tenants,
            normalizedMonth,
            request.AllowDuplicateForMonth,
            request.PreviewOnly,
            isCustom: false,
            customRequest: null,
            AdminAuditActionType.BillingInvoiceBulkGenerated,
            cancellationToken);
    }

    public async Task<PortalAdminBillingGenerationResultDto> CreateCustomInvoicesAsync(PortalAdminBillingCustomInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var tenantIds = request.TenantIds.Distinct().ToArray();
        var tenants = await GetBillableTenantsQuery()
            .Where(x => tenantIds.Contains(x.Id))
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        if (tenants.Count == 0)
        {
            return new PortalAdminBillingGenerationResultDto
            {
                PreviewOnly = request.PreviewOnly,
                GeneratedCount = 0,
                SkippedCount = 0,
                Invoices = Array.Empty<PortalAdminBillingGeneratedInvoiceDto>(),
                Skipped = Array.Empty<PortalAdminBillingSkippedInvoiceDto>()
            };
        }

        return await GenerateInvoicesCoreAsync(
            actor,
            tenants,
            new DateOnly(request.BillingMonth.Year, request.BillingMonth.Month, 1),
            request.AllowDuplicateForMonth,
            request.PreviewOnly,
            isCustom: true,
            customRequest: request,
            AdminAuditActionType.BillingInvoiceCustomGenerated,
            cancellationToken);
    }

    public async Task<int> DeleteAllInvoicesAsync(CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var deletedEmailLogCount = await _dbContext.AdminInvoiceEmailLogs
            .IgnoreQueryFilters()
            .ExecuteDeleteAsync(cancellationToken);

        var deletedLineItemCount = await _dbContext.AdminInvoiceLineItems
            .IgnoreQueryFilters()
            .ExecuteDeleteAsync(cancellationToken);

        var deletedInvoiceCount = await _dbContext.AdminInvoices
            .IgnoreQueryFilters()
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.BillingInvoicesReset,
            nameof(AdminInvoice),
            null,
            $"{deletedInvoiceCount} invoice(s)",
            null,
            new
            {
                DeletedInvoices = deletedInvoiceCount,
                DeletedLineItems = deletedLineItemCount,
                DeletedEmailLogs = deletedEmailLogCount
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return deletedInvoiceCount;
    }

    public async Task SendInvoiceEmailAsync(Guid invoiceId, PortalAdminBillingSendInvoiceEmailRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var invoice = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Include(x => x.LineItems)
            .Include(x => x.EmailLogs)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Billing invoice not found.");

        var toEmail = string.IsNullOrWhiteSpace(request.ToEmail)
            ? invoice.CompanyEmailSnapshot
            : request.ToEmail.Trim();
        var ccEmail = string.IsNullOrWhiteSpace(request.CcEmail)
            ? invoice.CompanyAdminEmailSnapshot
            : request.CcEmail.Trim();

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new AppException("Company email is required to send invoice.");
        }

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var settingsDto = MapSettings(settings);
        var detailDto = MapInvoiceDetail(invoice);
        var pdfBytes = _pdfExportService.BuildPortalAdminInvoicePdf(detailDto, settingsDto);

        var subject = $"Invoice for {invoice.BillingMonth:MMMM yyyy} - {invoice.CompanyNameSnapshot}";
        var attachmentName = $"admin-invoice-{invoice.InvoiceNumber}.pdf";
        var now = DateTimeOffset.UtcNow;

        try
        {
            await _notificationService.SendPortalAdminInvoiceAsync(
                toEmail,
                ccEmail,
                invoice.CompanyNameSnapshot,
                invoice.BillingMonth,
                subject,
                pdfBytes,
                attachmentName,
                settings.EmailFromName,
                settings.ReplyToEmail,
                cancellationToken);

            invoice.SentAt = now;
            invoice.Status = AdminInvoiceStatus.Emailed;

            _dbContext.AdminInvoiceEmailLogs.Add(new AdminInvoiceEmailLog
            {
                AdminInvoiceId = invoice.Id,
                ToEmail = toEmail,
                CcEmail = string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail,
                Subject = subject,
                AttemptedAt = now,
                Status = AdminInvoiceEmailStatus.Sent,
                AttemptedByUserId = actor.Id
            });

            _dbContext.AdminAuditLogs.Add(CreateAuditLog(
                actor.Id,
                AdminAuditActionType.BillingInvoiceEmailed,
                nameof(AdminInvoice),
                invoice.Id.ToString(),
                invoice.InvoiceNumber,
                invoice.TenantId,
                new { toEmail, ccEmail, subject }));

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            invoice.Status = AdminInvoiceStatus.EmailFailed;
            _dbContext.AdminInvoiceEmailLogs.Add(new AdminInvoiceEmailLog
            {
                AdminInvoiceId = invoice.Id,
                ToEmail = toEmail,
                CcEmail = string.IsNullOrWhiteSpace(ccEmail) ? null : ccEmail,
                Subject = subject,
                AttemptedAt = now,
                Status = AdminInvoiceEmailStatus.Failed,
                ErrorMessage = Truncate(exception.Message, 1200),
                AttemptedByUserId = actor.Id
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (exception is AppException appException)
            {
                throw appException;
            }

            throw new AppException("Unable to send billing invoice email.");
        }
    }

    public async Task<byte[]> GetInvoicePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);
        var invoice = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Include(x => x.LineItems)
            .Include(x => x.EmailLogs)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Billing invoice not found.");

        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        return _pdfExportService.BuildPortalAdminInvoicePdf(MapInvoiceDetail(invoice), MapSettings(settings));
    }

    public async Task<PagedResult<PortalAdminBillingBusinessCustomRateDto>> GetCustomRatesAsync(PortalAdminBillingCustomRateQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureSuperAdminAsync(cancellationToken);

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var ratesQuery = _dbContext.BusinessCustomRates.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted);

        if (query.IsActive.HasValue)
        {
            ratesQuery = ratesQuery.Where(x => x.IsActive == query.IsActive.Value);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            ratesQuery = ratesQuery.Where(x => x.Tenant.CompanyName.ToLower().Contains(search) || x.Tenant.CompanyEmail.ToLower().Contains(search));
        }

        var totalCount = await ratesQuery.CountAsync(cancellationToken);
        var items = await ratesQuery
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Tenant.CompanyName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PortalAdminBillingBusinessCustomRateDto
            {
                Id = x.Id,
                TenantId = x.TenantId,
                CompanyName = x.Tenant.CompanyName,
                SoftwareFee = x.SoftwareFee,
                VesselFee = x.VesselFee,
                StaffFee = x.StaffFee,
                Currency = x.Currency == "USD" ? "USD" : "MVR",
                IsActive = x.IsActive,
                EffectiveFrom = x.EffectiveFrom,
                EffectiveTo = x.EffectiveTo,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PortalAdminBillingBusinessCustomRateDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PortalAdminBillingBusinessCustomRateDto> CreateCustomRateAsync(PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var tenant = await GetBillableTenantByIdAsync(request.TenantId, cancellationToken);

        var rate = new BusinessCustomRate
        {
            TenantId = tenant.Id,
            SoftwareFee = RoundMoney(request.SoftwareFee),
            VesselFee = RoundMoney(request.VesselFee),
            StaffFee = RoundMoney(request.StaffFee),
            Currency = NormalizeCurrency(request.Currency, "MVR"),
            IsActive = request.IsActive,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Notes = TrimOrNull(request.Notes)
        };

        _dbContext.BusinessCustomRates.Add(rate);
        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.BillingCustomRateCreated,
            nameof(BusinessCustomRate),
            rate.Id.ToString(),
            tenant.CompanyName,
            tenant.Id,
            new
            {
                rate.SoftwareFee,
                rate.VesselFee,
                rate.StaffFee,
                rate.Currency,
                rate.IsActive,
                rate.EffectiveFrom,
                rate.EffectiveTo
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PortalAdminBillingBusinessCustomRateDto
        {
            Id = rate.Id,
            TenantId = rate.TenantId,
            CompanyName = tenant.CompanyName,
            SoftwareFee = rate.SoftwareFee,
            VesselFee = rate.VesselFee,
            StaffFee = rate.StaffFee,
            Currency = rate.Currency,
            IsActive = rate.IsActive,
            EffectiveFrom = rate.EffectiveFrom,
            EffectiveTo = rate.EffectiveTo,
            Notes = rate.Notes
        };
    }

    public async Task<PortalAdminBillingBusinessCustomRateDto> UpdateCustomRateAsync(Guid rateId, PortalAdminBillingUpsertCustomRateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var rate = await _dbContext.BusinessCustomRates.IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == rateId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Custom billing rate not found.");

        if (rate.TenantId != request.TenantId)
        {
            await GetBillableTenantByIdAsync(request.TenantId, cancellationToken);
            rate.TenantId = request.TenantId;
        }

        rate.SoftwareFee = RoundMoney(request.SoftwareFee);
        rate.VesselFee = RoundMoney(request.VesselFee);
        rate.StaffFee = RoundMoney(request.StaffFee);
        rate.Currency = NormalizeCurrency(request.Currency, "MVR");
        rate.IsActive = request.IsActive;
        rate.EffectiveFrom = request.EffectiveFrom;
        rate.EffectiveTo = request.EffectiveTo;
        rate.Notes = TrimOrNull(request.Notes);

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.BillingCustomRateUpdated,
            nameof(BusinessCustomRate),
            rate.Id.ToString(),
            rate.Tenant.CompanyName,
            rate.TenantId,
            new
            {
                rate.SoftwareFee,
                rate.VesselFee,
                rate.StaffFee,
                rate.Currency,
                rate.IsActive,
                rate.EffectiveFrom,
                rate.EffectiveTo
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);

        var companyName = await _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => x.Id == rate.TenantId)
            .Select(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken) ?? rate.Tenant.CompanyName;

        return new PortalAdminBillingBusinessCustomRateDto
        {
            Id = rate.Id,
            TenantId = rate.TenantId,
            CompanyName = companyName,
            SoftwareFee = rate.SoftwareFee,
            VesselFee = rate.VesselFee,
            StaffFee = rate.StaffFee,
            Currency = rate.Currency,
            IsActive = rate.IsActive,
            EffectiveFrom = rate.EffectiveFrom,
            EffectiveTo = rate.EffectiveTo,
            Notes = rate.Notes
        };
    }

    public async Task DeleteCustomRateAsync(Guid rateId, CancellationToken cancellationToken = default)
    {
        var actor = await EnsureSuperAdminAsync(cancellationToken);
        var rate = await _dbContext.BusinessCustomRates.IgnoreQueryFilters()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == rateId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Custom billing rate not found.");

        _dbContext.BusinessCustomRates.Remove(rate);
        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            AdminAuditActionType.BillingCustomRateDeleted,
            nameof(BusinessCustomRate),
            rate.Id.ToString(),
            rate.Tenant.CompanyName,
            rate.TenantId));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PortalAdminBillingGenerationResultDto> GenerateInvoicesCoreAsync(
        User actor,
        IReadOnlyList<Tenant> tenants,
        DateOnly billingMonth,
        bool allowDuplicateForMonth,
        bool previewOnly,
        bool isCustom,
        PortalAdminBillingCustomInvoiceRequest? customRequest,
        AdminAuditActionType actionType,
        CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateSettingsAsync(cancellationToken);
        var normalizedMonth = new DateOnly(billingMonth.Year, billingMonth.Month, 1);

        if (tenants.Count == 0)
        {
            return new PortalAdminBillingGenerationResultDto
            {
                PreviewOnly = previewOnly,
                GeneratedCount = 0,
                SkippedCount = 0
            };
        }

        var tenantIds = tenants.Select(x => x.Id).ToArray();
        var staffCounts = await _dbContext.Staff.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var vesselCounts = await _dbContext.Vessels.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var primaryAdmins = await _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && tenantIds.Contains(x.TenantId) && x.Role.Name == UserRoleName.Admin)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.TenantId,
                x.FullName,
                x.Email
            })
            .ToListAsync(cancellationToken);

        var adminLookup = primaryAdmins
            .GroupBy(x => x.TenantId)
            .ToDictionary(x => x.Key, x => x.First());

        var activeCustomRates = await _dbContext.BusinessCustomRates.IgnoreQueryFilters()
            .Where(x =>
                !x.IsDeleted
                && x.IsActive
                && tenantIds.Contains(x.TenantId)
                && (!x.EffectiveFrom.HasValue || x.EffectiveFrom.Value <= normalizedMonth)
                && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= normalizedMonth))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var customRateLookup = activeCustomRates
            .GroupBy(x => x.TenantId)
            .ToDictionary(x => x.Key, x => x.First());

        var duplicateTenantIds = allowDuplicateForMonth
            ? new HashSet<Guid>()
            : (await _dbContext.AdminInvoices.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.BillingMonth == normalizedMonth && tenantIds.Contains(x.TenantId) && x.Status != AdminInvoiceStatus.Cancelled)
                .Select(x => x.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var preparedInvoices = new List<PreparedInvoice>(tenants.Count);
        var skipped = new List<PortalAdminBillingSkippedInvoiceDto>();

        var invoiceDate = isCustom && customRequest?.InvoiceDate is not null
            ? customRequest.InvoiceDate.Value
            : DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = isCustom && customRequest?.DueDate is not null
            ? customRequest.DueDate.Value
            : invoiceDate.AddDays(settings.DefaultDueDays);

        foreach (var tenant in tenants)
        {
            if (!allowDuplicateForMonth && duplicateTenantIds.Contains(tenant.Id))
            {
                skipped.Add(new PortalAdminBillingSkippedInvoiceDto
                {
                    TenantId = tenant.Id,
                    CompanyName = tenant.CompanyName,
                    Reason = "Invoice already exists for this business and billing month."
                });
                continue;
            }

            var tenantCustomRate = customRateLookup.GetValueOrDefault(tenant.Id);
            var baseSoftwareFee = tenantCustomRate?.SoftwareFee ?? settings.BasicSoftwareFee;
            var vesselRate = tenantCustomRate?.VesselFee ?? settings.VesselFee;
            var staffRate = tenantCustomRate?.StaffFee ?? settings.StaffFee;
            var currency = NormalizeCurrency(tenantCustomRate?.Currency, settings.DefaultCurrency);

            if (isCustom && customRequest is not null)
            {
                if (customRequest.SoftwareFee.HasValue)
                {
                    baseSoftwareFee = customRequest.SoftwareFee.Value;
                }

                if (customRequest.VesselFee.HasValue)
                {
                    vesselRate = customRequest.VesselFee.Value;
                }

                if (customRequest.StaffFee.HasValue)
                {
                    staffRate = customRequest.StaffFee.Value;
                }

                if (!string.IsNullOrWhiteSpace(customRequest.Currency))
                {
                    currency = NormalizeCurrency(customRequest.Currency, currency);
                }
            }

            var staffCount = staffCounts.GetValueOrDefault(tenant.Id);
            var vesselCount = vesselCounts.GetValueOrDefault(tenant.Id);

            baseSoftwareFee = RoundMoney(baseSoftwareFee);
            vesselRate = RoundMoney(vesselRate);
            staffRate = RoundMoney(staffRate);

            var vesselAmount = RoundMoney(vesselRate * vesselCount);
            var staffAmount = RoundMoney(staffRate * staffCount);

            var lineItems = new List<PreparedLineItem>
            {
                new()
                {
                    Description = "Basic Software Fee",
                    Quantity = 1,
                    Rate = baseSoftwareFee,
                    Amount = baseSoftwareFee,
                    SortOrder = 1
                },
                new()
                {
                    Description = "Vessel Charges",
                    Quantity = vesselCount,
                    Rate = vesselRate,
                    Amount = vesselAmount,
                    SortOrder = 2
                },
                new()
                {
                    Description = "Staff Charges",
                    Quantity = staffCount,
                    Rate = staffRate,
                    Amount = staffAmount,
                    SortOrder = 3
                }
            };

            if (isCustom && customRequest is not null)
            {
                var sortOrder = lineItems.Count + 1;
                foreach (var item in customRequest.LineItems)
                {
                    lineItems.Add(new PreparedLineItem
                    {
                        Description = item.Description.Trim(),
                        Quantity = item.Quantity,
                        Rate = RoundMoney(item.Rate),
                        Amount = RoundMoney(item.Quantity * item.Rate),
                        SortOrder = sortOrder++
                    });
                }
            }

            var subtotal = RoundMoney(lineItems.Sum(x => x.Amount));
            var total = subtotal;
            var admin = adminLookup.GetValueOrDefault(tenant.Id);

            preparedInvoices.Add(new PreparedInvoice
            {
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                CompanyEmail = tenant.CompanyEmail,
                CompanyPhone = tenant.CompanyPhone,
                TinNumber = tenant.TinNumber,
                BusinessRegistrationNumber = tenant.BusinessRegistrationNumber,
                CompanyAdminName = admin?.FullName,
                CompanyAdminEmail = admin?.Email,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                BillingMonth = normalizedMonth,
                Currency = currency,
                BaseSoftwareFee = baseSoftwareFee,
                VesselCount = vesselCount,
                VesselRate = vesselRate,
                VesselAmount = vesselAmount,
                StaffCount = staffCount,
                StaffRate = staffRate,
                StaffAmount = staffAmount,
                Subtotal = subtotal,
                Total = total,
                Notes = isCustom ? TrimOrNull(customRequest?.Notes) : null,
                IsCustom = isCustom,
                CustomRateId = tenantCustomRate?.Id,
                LineItems = lineItems
            });
        }

        var invoicePrefix = NormalizePrefix(settings.InvoicePrefix);
        if (previewOnly)
        {
            var nextSequence = await ResolveNextSequenceAsync(invoicePrefix, normalizedMonth, settings.StartingSequenceNumber, cancellationToken);
            var previewRows = preparedInvoices.Select((prepared, index) => new PortalAdminBillingGeneratedInvoiceDto
            {
                InvoiceId = null,
                InvoiceNumber = BuildInvoiceNumber(invoicePrefix, normalizedMonth, nextSequence + index),
                TenantId = prepared.TenantId,
                CompanyName = prepared.CompanyName,
                BillingMonth = prepared.BillingMonth,
                StaffCount = prepared.StaffCount,
                VesselCount = prepared.VesselCount,
                Total = prepared.Total,
                Currency = prepared.Currency
            }).ToList();

            return new PortalAdminBillingGenerationResultDto
            {
                PreviewOnly = true,
                GeneratedCount = previewRows.Count,
                SkippedCount = skipped.Count,
                Invoices = previewRows,
                Skipped = skipped
            };
        }

        var createdRows = new List<PortalAdminBillingGeneratedInvoiceDto>();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({SequenceLockKey});", cancellationToken);

        if (!allowDuplicateForMonth && preparedInvoices.Count > 0)
        {
            var currentTenantIds = preparedInvoices.Select(x => x.TenantId).Distinct().ToArray();
            var nowDuplicateTenantIds = await _dbContext.AdminInvoices.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && x.BillingMonth == normalizedMonth && currentTenantIds.Contains(x.TenantId) && x.Status != AdminInvoiceStatus.Cancelled)
                .Select(x => x.TenantId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (nowDuplicateTenantIds.Count > 0)
            {
                preparedInvoices = preparedInvoices
                    .Where(x =>
                    {
                        if (!nowDuplicateTenantIds.Contains(x.TenantId))
                        {
                            return true;
                        }

                        skipped.Add(new PortalAdminBillingSkippedInvoiceDto
                        {
                            TenantId = x.TenantId,
                            CompanyName = x.CompanyName,
                            Reason = "Invoice already exists for this business and billing month."
                        });
                        return false;
                    })
                    .ToList();
            }
        }

        var nextSequenceForCreate = await ResolveNextSequenceAsync(invoicePrefix, normalizedMonth, settings.StartingSequenceNumber, cancellationToken);
        for (var index = 0; index < preparedInvoices.Count; index++)
        {
            var prepared = preparedInvoices[index];
            var invoiceNumber = BuildInvoiceNumber(invoicePrefix, normalizedMonth, nextSequenceForCreate + index);
            var invoice = new AdminInvoice
            {
                InvoiceNumber = invoiceNumber,
                TenantId = prepared.TenantId,
                BillingMonth = prepared.BillingMonth,
                InvoiceDate = prepared.InvoiceDate,
                DueDate = prepared.DueDate,
                Currency = prepared.Currency,
                CompanyNameSnapshot = prepared.CompanyName,
                CompanyEmailSnapshot = prepared.CompanyEmail,
                CompanyPhoneSnapshot = prepared.CompanyPhone,
                CompanyTinSnapshot = prepared.TinNumber,
                CompanyRegistrationSnapshot = prepared.BusinessRegistrationNumber,
                CompanyAdminNameSnapshot = prepared.CompanyAdminName,
                CompanyAdminEmailSnapshot = prepared.CompanyAdminEmail,
                BaseSoftwareFee = prepared.BaseSoftwareFee,
                VesselCount = prepared.VesselCount,
                VesselRate = prepared.VesselRate,
                VesselAmount = prepared.VesselAmount,
                StaffCount = prepared.StaffCount,
                StaffRate = prepared.StaffRate,
                StaffAmount = prepared.StaffAmount,
                Subtotal = prepared.Subtotal,
                Total = prepared.Total,
                Notes = prepared.Notes,
                Status = AdminInvoiceStatus.Issued,
                IsCustom = prepared.IsCustom,
                SentAt = null,
                CustomRateId = prepared.CustomRateId
            };

            foreach (var lineItem in prepared.LineItems)
            {
                invoice.LineItems.Add(new AdminInvoiceLineItem
                {
                    Description = lineItem.Description,
                    Quantity = lineItem.Quantity,
                    Rate = lineItem.Rate,
                    Amount = lineItem.Amount,
                    SortOrder = lineItem.SortOrder
                });
            }

            _dbContext.AdminInvoices.Add(invoice);
            createdRows.Add(new PortalAdminBillingGeneratedInvoiceDto
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoiceNumber,
                TenantId = prepared.TenantId,
                CompanyName = prepared.CompanyName,
                BillingMonth = prepared.BillingMonth,
                StaffCount = prepared.StaffCount,
                VesselCount = prepared.VesselCount,
                Total = prepared.Total,
                Currency = prepared.Currency
            });
        }

        if (isCustom && customRequest is not null && customRequest.SaveAsBusinessCustomRate && preparedInvoices.Count > 0)
        {
            var tenantIdSet = preparedInvoices.Select(x => x.TenantId).Distinct().ToArray();
            var existingActiveRates = await _dbContext.BusinessCustomRates.IgnoreQueryFilters()
                .Where(x => !x.IsDeleted && tenantIdSet.Contains(x.TenantId) && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var prepared in preparedInvoices)
            {
                var existingRate = existingActiveRates.FirstOrDefault(x => x.TenantId == prepared.TenantId);
                if (existingRate is null)
                {
                    _dbContext.BusinessCustomRates.Add(new BusinessCustomRate
                    {
                        TenantId = prepared.TenantId,
                        SoftwareFee = prepared.BaseSoftwareFee,
                        VesselFee = prepared.VesselRate,
                        StaffFee = prepared.StaffRate,
                        Currency = prepared.Currency,
                        IsActive = true,
                        Notes = TrimOrNull(customRequest.Notes)
                    });
                }
                else
                {
                    existingRate.SoftwareFee = prepared.BaseSoftwareFee;
                    existingRate.VesselFee = prepared.VesselRate;
                    existingRate.StaffFee = prepared.StaffRate;
                    existingRate.Currency = prepared.Currency;
                    existingRate.IsActive = true;
                    existingRate.Notes = TrimOrNull(customRequest.Notes);
                }
            }
        }

        _dbContext.AdminAuditLogs.Add(CreateAuditLog(
            actor.Id,
            actionType,
            nameof(AdminInvoice),
            null,
            $"{createdRows.Count} invoice(s)",
            createdRows.Count == 1 ? createdRows[0].TenantId : null,
            new
            {
                BillingMonth = normalizedMonth.ToString("yyyy-MM"),
                PreviewOnly = false,
                Generated = createdRows.Count,
                Skipped = skipped.Count,
                TenantIds = createdRows.Select(x => x.TenantId).Distinct().ToArray(),
                IsCustom = isCustom
            }));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PortalAdminBillingGenerationResultDto
        {
            PreviewOnly = false,
            GeneratedCount = createdRows.Count,
            SkippedCount = skipped.Count,
            Invoices = createdRows,
            Skipped = skipped
        };
    }

    private async Task<User> EnsureSuperAdminAsync(CancellationToken cancellationToken)
    {
        var context = _currentUserService.GetContext();
        if (context.UserId is null || !string.Equals(context.Role, UserRoleName.SuperAdmin, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Portal admin access is required.");
        }

        var user = await _dbContext.Users.IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedException("Unauthorized.");

        if (user.Role.Name != UserRoleName.SuperAdmin)
        {
            throw new ForbiddenException("Portal admin access is required.");
        }

        return user;
    }

    private IQueryable<Tenant> GetBillableTenantsQuery()
    {
        var superAdminTenantIds = SuperAdminTenantIdsQuery();
        return _dbContext.Tenants.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && !superAdminTenantIds.Contains(x.Id));
    }

    private async Task<Tenant> GetBillableTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await GetBillableTenantsQuery()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        return tenant ?? throw new NotFoundException("Business not found.");
    }

    private IQueryable<Guid> SuperAdminTenantIdsQuery()
    {
        return _dbContext.Users.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.Role.Name == UserRoleName.SuperAdmin)
            .Select(x => x.TenantId)
            .Distinct();
    }

    private async Task<AdminBillingSettings> GetOrCreateSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.AdminBillingSettings.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new AdminBillingSettings
        {
            BasicSoftwareFee = DefaultSoftwareFee,
            VesselFee = DefaultVesselFee,
            StaffFee = DefaultStaffFee,
            InvoicePrefix = "ADM",
            StartingSequenceNumber = 1,
            DefaultCurrency = "MVR",
            DefaultDueDays = DefaultDueDays,
            AccountName = "myDhathuru",
            AccountNumber = "N/A",
            PaymentInstructions = "Please settle invoice at your earliest convenience.",
            InvoiceFooterNote = "Thank you for using myDhathuru platform.",
            InvoiceTerms = "Invoices are issued in advance for the selected billing month.",
            LogoUrl = DefaultInvoiceLogoUrl,
            EmailFromName = "myDhathuru Billing"
        };

        _dbContext.AdminBillingSettings.Add(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<int> ResolveNextSequenceAsync(string prefix, DateOnly billingMonth, int startingSequenceNumber, CancellationToken cancellationToken)
    {
        var monthPrefix = $"{prefix}-{billingMonth:yyyyMM}-";
        var existingNumbers = await _dbContext.AdminInvoices.IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.InvoiceNumber.StartsWith(monthPrefix))
            .Select(x => x.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var maxSequence = startingSequenceNumber - 1;
        foreach (var invoiceNumber in existingNumbers)
        {
            if (invoiceNumber.Length <= monthPrefix.Length)
            {
                continue;
            }

            var suffix = invoiceNumber[monthPrefix.Length..];
            if (int.TryParse(suffix, out var parsed) && parsed > maxSequence)
            {
                maxSequence = parsed;
            }
        }

        return maxSequence + 1;
    }

    private static string BuildInvoiceNumber(string prefix, DateOnly billingMonth, int sequence)
    {
        return $"{prefix}-{billingMonth:yyyyMM}-{sequence:0000}";
    }

    private PortalAdminBillingInvoiceDetailDto MapInvoiceDetail(AdminInvoice invoice)
    {
        return new PortalAdminBillingInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            BillingMonth = invoice.BillingMonth,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            TenantId = invoice.TenantId,
            CompanyName = invoice.CompanyNameSnapshot,
            Total = invoice.Total,
            Currency = NormalizeCurrency(invoice.Currency, "MVR"),
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            SentAt = invoice.SentAt,
            IsCustom = invoice.IsCustom,
            CompanyEmail = invoice.CompanyEmailSnapshot,
            CompanyPhone = invoice.CompanyPhoneSnapshot,
            CompanyTinNumber = invoice.CompanyTinSnapshot,
            CompanyRegistrationNumber = invoice.CompanyRegistrationSnapshot,
            CompanyAdminName = invoice.CompanyAdminNameSnapshot,
            CompanyAdminEmail = invoice.CompanyAdminEmailSnapshot,
            BaseSoftwareFee = invoice.BaseSoftwareFee,
            VesselCount = invoice.VesselCount,
            VesselRate = invoice.VesselRate,
            VesselAmount = invoice.VesselAmount,
            StaffCount = invoice.StaffCount,
            StaffRate = invoice.StaffRate,
            StaffAmount = invoice.StaffAmount,
            Subtotal = invoice.Subtotal,
            Notes = invoice.Notes,
            LineItems = invoice.LineItems
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.CreatedAt)
                .Select(x => new PortalAdminBillingInvoiceLineItemDto
                {
                    Id = x.Id,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    Rate = x.Rate,
                    Amount = x.Amount,
                    SortOrder = x.SortOrder
                })
                .ToList(),
            EmailLogs = invoice.EmailLogs
                .OrderByDescending(x => x.AttemptedAt)
                .Select(x => new PortalAdminBillingInvoiceEmailLogDto
                {
                    Id = x.Id,
                    ToEmail = x.ToEmail,
                    CcEmail = x.CcEmail,
                    Subject = x.Subject,
                    AttemptedAt = x.AttemptedAt,
                    Status = x.Status,
                    ErrorMessage = x.ErrorMessage
                })
                .ToList()
        };
    }

    private static PortalAdminBillingSettingsDto MapSettings(AdminBillingSettings settings)
    {
        return new PortalAdminBillingSettingsDto
        {
            BasicSoftwareFee = settings.BasicSoftwareFee,
            VesselFee = settings.VesselFee,
            StaffFee = settings.StaffFee,
            InvoicePrefix = settings.InvoicePrefix,
            StartingSequenceNumber = settings.StartingSequenceNumber,
            DefaultCurrency = NormalizeCurrency(settings.DefaultCurrency, "MVR"),
            DefaultDueDays = settings.DefaultDueDays,
            AccountName = settings.AccountName,
            AccountNumber = settings.AccountNumber,
            BankName = settings.BankName,
            Branch = settings.Branch,
            PaymentInstructions = settings.PaymentInstructions,
            InvoiceFooterNote = settings.InvoiceFooterNote,
            InvoiceTerms = settings.InvoiceTerms,
            LogoUrl = string.IsNullOrWhiteSpace(settings.LogoUrl) ? DefaultInvoiceLogoUrl : settings.LogoUrl,
            EmailFromName = settings.EmailFromName,
            ReplyToEmail = settings.ReplyToEmail,
            AutoGenerationEnabled = settings.AutoGenerationEnabled,
            AutoEmailEnabled = settings.AutoEmailEnabled
        };
    }

    private AdminAuditLog CreateAuditLog(
        Guid performedByUserId,
        AdminAuditActionType actionType,
        string targetType,
        string? targetId,
        string? targetName,
        Guid? relatedTenantId,
        object? details = null)
    {
        return new AdminAuditLog
        {
            PerformedByUserId = performedByUserId,
            ActionType = actionType,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            RelatedTenantId = relatedTenantId,
            Details = details is null ? null : JsonSerializer.Serialize(details),
            PerformedAt = DateTimeOffset.UtcNow
        };
    }

    private static (decimal Mvr, decimal Usd) BuildCurrencyTotals(IEnumerable<(string Currency, decimal Amount)> rows)
    {
        decimal mvr = 0m;
        decimal usd = 0m;

        foreach (var (currency, amount) in rows)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                usd += amount;
            }
            else
            {
                mvr += amount;
            }
        }

        return (RoundMoney(mvr), RoundMoney(usd));
    }

    private static string NormalizeCurrency(string? requestedCurrency, string fallbackCurrency)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedCurrency)
            ? fallbackCurrency.Trim().ToUpperInvariant()
            : requestedCurrency.Trim().ToUpperInvariant();

        if (normalized is not ("MVR" or "USD"))
        {
            throw new AppException("Currency must be MVR or USD.");
        }

        return normalized;
    }

    private static string NormalizePrefix(string? prefix)
    {
        var normalized = string.IsNullOrWhiteSpace(prefix) ? "ADM" : prefix.Trim().ToUpperInvariant();
        return normalized.Length > 20 ? normalized[..20] : normalized;
    }

    private static decimal RoundMoney(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private sealed class PreparedInvoice
    {
        public Guid TenantId { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public string CompanyEmail { get; init; } = string.Empty;
        public string CompanyPhone { get; init; } = string.Empty;
        public string TinNumber { get; init; } = string.Empty;
        public string BusinessRegistrationNumber { get; init; } = string.Empty;
        public string? CompanyAdminName { get; init; }
        public string? CompanyAdminEmail { get; init; }
        public DateOnly BillingMonth { get; init; }
        public DateOnly InvoiceDate { get; init; }
        public DateOnly DueDate { get; init; }
        public string Currency { get; init; } = "MVR";
        public decimal BaseSoftwareFee { get; init; }
        public int VesselCount { get; init; }
        public decimal VesselRate { get; init; }
        public decimal VesselAmount { get; init; }
        public int StaffCount { get; init; }
        public decimal StaffRate { get; init; }
        public decimal StaffAmount { get; init; }
        public decimal Subtotal { get; init; }
        public decimal Total { get; init; }
        public string? Notes { get; init; }
        public bool IsCustom { get; init; }
        public Guid? CustomRateId { get; init; }
        public IReadOnlyList<PreparedLineItem> LineItems { get; init; } = Array.Empty<PreparedLineItem>();
    }

    private sealed class PreparedLineItem
    {
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal Rate { get; init; }
        public decimal Amount { get; init; }
        public int SortOrder { get; init; }
    }
}
