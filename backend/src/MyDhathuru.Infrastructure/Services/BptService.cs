using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Bpt.Dtos;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class BptService : IBptService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";

    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPdfExportService _pdfExportService;

    public BptService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        IDocumentNumberService documentNumberService,
        IBusinessAuditLogService auditLogService,
        IPdfExportService pdfExportService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _documentNumberService = documentNumberService;
        _auditLogService = auditLogService;
        _pdfExportService = pdfExportService;
    }

    public async Task<BptReportDto> GetReportAsync(BptReportQuery query, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var range = ResolveRange(query);
        await EnsureDefaultBptSetupAsync(cancellationToken);

        var mappingRules = await _dbContext.BptMappingRules
            .AsNoTracking()
            .Include(x => x.BptCategory)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var exchangeRates = await _dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.IsActive && x.RateDate <= range.EndDate)
            .OrderBy(x => x.RateDate)
            .ToListAsync(cancellationToken);

        var context = new BptBuildContext();
        var transactions = new List<BptTraceTransactionDto>();
        transactions.AddRange(await BuildInvoiceTransactionsAsync(range, mappingRules, exchangeRates, context, cancellationToken));
        transactions.AddRange(await BuildSalesAdjustmentTransactionsAsync(range, mappingRules, context, cancellationToken));
        transactions.AddRange(await BuildOtherIncomeTransactionsAsync(range, mappingRules, context, cancellationToken));
        transactions.AddRange(await BuildReceivedInvoiceTransactionsAsync(range, mappingRules, exchangeRates, context, cancellationToken));
        transactions.AddRange(await BuildRentTransactionsAsync(range, mappingRules, exchangeRates, context, cancellationToken));
        transactions.AddRange(await BuildManualExpenseTransactionsAsync(range, mappingRules, exchangeRates, context, cancellationToken));
        transactions.AddRange(await BuildPayrollTransactionsAsync(range, mappingRules, context, cancellationToken));
        transactions.AddRange(await BuildAdjustmentTransactionsAsync(range, context, cancellationToken));

        if (context.MissingExchangeRateMessages.Count > 0)
        {
            var preview = string.Join(", ", context.MissingExchangeRateMessages.Take(6));
            throw new AppException($"Exchange rates are missing for: {preview}. Add the missing rate entries in BPT before generating the statement.");
        }

        transactions = transactions
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.SourceModule.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceDocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var notes = new BptNotesDto
        {
            SalaryRows = await BuildSalaryNotesAsync(range, context.UseDraftPayrollPeriods, cancellationToken),
            Sections = BuildNoteSections(transactions)
        };
        notes.TotalSalary = Round2(notes.SalaryRows.Sum(x => x.TotalForPeriodRange));

        return new BptReportDto
        {
            Meta = new BptReportMetaDto
            {
                Title = "Business Profit Tax Statement",
                PeriodLabel = range.Label,
                RangeStart = range.StartDate,
                RangeEnd = range.EndDate,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                TaxableActivityNumber = Safe(settings.TaxableActivityNumber),
                CompanyName = settings.CompanyName,
                CompanyTinNumber = settings.TinNumber
            },
            ImportantNotes = BuildImportantNotes(context, transactions),
            Statement = BuildStatement(transactions),
            Notes = notes,
            Transactions = transactions
        };
    }

    public async Task<ReportExportResultDto> ExportExcelAsync(BptReportExportRequest request, CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(request, cancellationToken);
        using var workbook = new XLWorkbook();

        BuildSummarySheet(workbook, report);
        BuildTransactionsSheet(workbook, report);
        BuildSalarySheet(workbook, report);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(report, "xlsx"),
            ContentType = ExcelContentType,
            Content = SaveWorkbook(workbook)
        };
    }

    public async Task<ReportExportResultDto> ExportPdfAsync(BptReportExportRequest request, CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(request, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = BuildCompanyInfo(settings.TinNumber, settings.CompanyPhone, settings.CompanyEmail);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(report, "pdf"),
            ContentType = PdfContentType,
            Content = _pdfExportService.BuildBptIncomeStatementPdf(
                MapToLegacyPreview(report),
                settings.CompanyName,
                companyInfo,
                settings.LogoUrl,
                settings.CompanyStampUrl,
                settings.CompanySignatureUrl)
        };
    }

    public async Task<IReadOnlyList<BptCategoryLookupDto>> GetCategoryLookupAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultBptSetupAsync(cancellationToken);

        return await _dbContext.BptCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new BptCategoryLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ClassificationGroup = x.ClassificationGroup
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BptExpenseMappingDto>> GetExpenseMappingsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultBptSetupAsync(cancellationToken);

        return await _dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .GroupJoin(
                _dbContext.BptMappingRules
                    .AsNoTracking()
                    .Include(x => x.BptCategory)
                    .Where(x => x.ExpenseCategoryId != null && x.SourceModule == null),
                category => category.Id,
                rule => rule.ExpenseCategoryId,
                (category, rules) => new { category, rule = rules.OrderBy(x => x.Priority).FirstOrDefault() })
            .OrderBy(x => x.category.SortOrder)
            .ThenBy(x => x.category.Name)
            .Select(x => new BptExpenseMappingDto
            {
                Id = x.rule != null ? x.rule.Id : null,
                ExpenseCategoryId = x.category.Id,
                ExpenseCategoryName = x.category.Name,
                ExpenseCategoryCode = x.category.Code,
                BptCategoryId = x.rule != null ? x.rule.BptCategoryId : Guid.Empty,
                BptCategoryCode = x.rule != null ? x.rule.BptCategory.Code : BptCategoryCode.Other,
                BptCategoryName = x.rule != null ? x.rule.BptCategory.Name : "Not mapped",
                ClassificationGroup = x.rule != null ? x.rule.BptCategory.ClassificationGroup : BptClassificationGroup.OperatingExpense,
                SourceModule = x.rule != null ? x.rule.SourceModule : null,
                IsSystem = x.rule != null && x.rule.IsSystem,
                IsActive = x.rule == null || x.rule.IsActive,
                Notes = x.rule != null ? x.rule.Notes : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<BptExpenseMappingDto> UpsertExpenseMappingAsync(Guid expenseCategoryId, UpsertBptExpenseMappingRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultBptSetupAsync(cancellationToken);

        var expenseCategory = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == expenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");
        var bptCategory = await _dbContext.BptCategories.FirstOrDefaultAsync(x => x.Id == request.BptCategoryId, cancellationToken)
            ?? throw new NotFoundException("BPT category not found.");

        if (bptCategory.ClassificationGroup is not BptClassificationGroup.OperatingExpense
            && bptCategory.ClassificationGroup is not BptClassificationGroup.CostOfGoodsSold
            && bptCategory.ClassificationGroup is not BptClassificationGroup.Excluded)
        {
            throw new AppException("Expense mappings can only target operating expense, cost of goods sold, or excluded BPT categories.");
        }

        var mapping = await _dbContext.BptMappingRules
            .Include(x => x.BptCategory)
            .FirstOrDefaultAsync(x => x.ExpenseCategoryId == expenseCategoryId && x.SourceModule == request.SourceModule, cancellationToken);

        if (mapping is null)
        {
            mapping = new BptMappingRule
            {
                Name = $"{expenseCategory.Name} mapping",
                ExpenseCategoryId = expenseCategoryId,
                SourceModule = request.SourceModule,
                BptCategoryId = bptCategory.Id,
                Priority = 100,
                IsSystem = false,
                IsActive = request.IsActive,
                Notes = request.Notes?.Trim()
            };
            _dbContext.BptMappingRules.Add(mapping);
        }
        else
        {
            mapping.BptCategoryId = bptCategory.Id;
            mapping.IsActive = request.IsActive;
            mapping.Notes = request.Notes?.Trim();
            mapping.IsSystem = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.BptMappingRuleUpdated,
            nameof(BptMappingRule),
            mapping.Id.ToString(),
            mapping.Name,
            new { ExpenseCategoryName = expenseCategory.Name, BptCategoryName = bptCategory.Name, request.SourceModule },
            cancellationToken);

        return new BptExpenseMappingDto
        {
            Id = mapping.Id,
            ExpenseCategoryId = expenseCategory.Id,
            ExpenseCategoryName = expenseCategory.Name,
            ExpenseCategoryCode = expenseCategory.Code,
            BptCategoryId = bptCategory.Id,
            BptCategoryCode = bptCategory.Code,
            BptCategoryName = bptCategory.Name,
            ClassificationGroup = bptCategory.ClassificationGroup,
            SourceModule = mapping.SourceModule,
            IsSystem = mapping.IsSystem,
            IsActive = mapping.IsActive,
            Notes = mapping.Notes
        };
    }

    public async Task<IReadOnlyList<BptExchangeRateDto>> GetExchangeRatesAsync(BptExchangeRateListQuery query, CancellationToken cancellationToken = default)
    {
        var rates = _dbContext.ExchangeRates.AsNoTracking();

        if (query.DateFrom.HasValue)
        {
            rates = rates.Where(x => x.RateDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            rates = rates.Where(x => x.RateDate <= query.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var currency = NormalizeCurrency(query.Currency, CurrencyCode.MVR.ToString());
            rates = rates.Where(x => x.Currency == currency);
        }

        var items = await rates
            .OrderByDescending(x => x.RateDate)
            .ThenBy(x => x.Currency)
            .ToListAsync(cancellationToken);

        return items.Select(MapExchangeRate).ToList();
    }

    public async Task<BptExchangeRateDto> CreateExchangeRateAsync(UpsertBptExchangeRateRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var currency = NormalizeCurrency(request.Currency, CurrencyCode.USD.ToString());
        var existingRate = await _dbContext.ExchangeRates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.RateDate == request.RateDate
                    && x.Currency == currency,
                cancellationToken);

        if (existingRate is not null && !existingRate.IsDeleted)
        {
            throw new AppException("An exchange rate already exists for that currency and date.");
        }

        if (existingRate is not null)
        {
            existingRate.RateDate = request.RateDate;
            existingRate.Currency = currency;
            existingRate.RateToMvr = RoundRate(request.RateToMvr);
            existingRate.Source = request.Source?.Trim();
            existingRate.Notes = request.Notes?.Trim();
            existingRate.IsActive = request.IsActive;
            existingRate.IsDeleted = false;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                BusinessAuditActionType.ExchangeRateCreated,
                nameof(ExchangeRate),
                existingRate.Id.ToString(),
                $"{existingRate.Currency} {existingRate.RateDate:yyyy-MM-dd}",
                new { existingRate.RateToMvr, RestoredFromDeleted = true },
                cancellationToken);

            return MapExchangeRate(existingRate);
        }

        var rate = new ExchangeRate
        {
            RateDate = request.RateDate,
            Currency = currency,
            RateToMvr = RoundRate(request.RateToMvr),
            Source = request.Source?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = request.IsActive
        };

        _dbContext.ExchangeRates.Add(rate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExchangeRateCreated,
            nameof(ExchangeRate),
            rate.Id.ToString(),
            $"{rate.Currency} {rate.RateDate:yyyy-MM-dd}",
            new { rate.RateToMvr },
            cancellationToken);

        return MapExchangeRate(rate);
    }

    public async Task<BptExchangeRateDto> UpdateExchangeRateAsync(Guid id, UpsertBptExchangeRateRequest request, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.ExchangeRates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Exchange rate not found.");

        var currency = NormalizeCurrency(request.Currency, rate.Currency);
        var exists = await _dbContext.ExchangeRates
            .IgnoreQueryFilters()
            .AnyAsync(
                x => x.TenantId == rate.TenantId
                    && x.Id != id
                    && x.RateDate == request.RateDate
                    && x.Currency == currency,
                cancellationToken);
        if (exists)
        {
            throw new AppException("An exchange rate already exists for that currency and date.");
        }

        rate.RateDate = request.RateDate;
        rate.Currency = currency;
        rate.RateToMvr = RoundRate(request.RateToMvr);
        rate.Source = request.Source?.Trim();
        rate.Notes = request.Notes?.Trim();
        rate.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExchangeRateUpdated,
            nameof(ExchangeRate),
            rate.Id.ToString(),
            $"{rate.Currency} {rate.RateDate:yyyy-MM-dd}",
            new { rate.RateToMvr },
            cancellationToken);

        return MapExchangeRate(rate);
    }

    public async Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rate = await _dbContext.ExchangeRates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Exchange rate not found.");

        _dbContext.ExchangeRates.Remove(rate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExchangeRateDeleted,
            nameof(ExchangeRate),
            rate.Id.ToString(),
            $"{rate.Currency} {rate.RateDate:yyyy-MM-dd}",
            null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SalesAdjustmentDto>> GetSalesAdjustmentsAsync(SalesAdjustmentListQuery query, CancellationToken cancellationToken = default)
    {
        var adjustments = _dbContext.SalesAdjustments.AsNoTracking();

        if (query.DateFrom.HasValue)
        {
            adjustments = adjustments.Where(x => x.TransactionDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            adjustments = adjustments.Where(x => x.TransactionDate <= query.DateTo.Value);
        }

        if (query.AdjustmentType.HasValue)
        {
            adjustments = adjustments.Where(x => x.AdjustmentType == query.AdjustmentType.Value);
        }

        if (query.ApprovalStatus.HasValue)
        {
            adjustments = adjustments.Where(x => x.ApprovalStatus == query.ApprovalStatus.Value);
        }

        var items = await adjustments
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapSalesAdjustment).ToList();
    }

    public async Task<SalesAdjustmentDto> CreateSalesAdjustmentAsync(CreateSalesAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = request.RelatedInvoiceId.HasValue
            ? await _dbContext.Invoices.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == request.RelatedInvoiceId.Value, cancellationToken)
            : null;
        var customer = request.CustomerId.HasValue
            ? await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken)
            : invoice?.Customer;

        var currency = NormalizeCurrency(request.Currency, invoice?.Currency ?? (await GetTenantSettingsAsync(cancellationToken)).DefaultCurrency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(Math.Abs(request.AmountOriginal));

        var entity = new SalesAdjustment
        {
            AdjustmentNumber = await _documentNumberService.GenerateAsync(DocumentType.SalesAdjustment, request.TransactionDate, cancellationToken),
            AdjustmentType = request.AdjustmentType,
            TransactionDate = request.TransactionDate,
            RelatedInvoiceId = invoice?.Id,
            RelatedInvoiceNumber = invoice?.InvoiceNo,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name,
            Currency = currency,
            ExchangeRate = exchangeRate,
            AmountOriginal = amountOriginal,
            AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate),
            ApprovalStatus = request.ApprovalStatus,
            Notes = request.Notes?.Trim()
        };

        _dbContext.SalesAdjustments.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.SalesAdjustmentCreated,
            nameof(SalesAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            new { entity.AdjustmentType, entity.AmountMvr, entity.Currency },
            cancellationToken);

        return MapSalesAdjustment(entity);
    }

    public async Task<SalesAdjustmentDto> UpdateSalesAdjustmentAsync(Guid id, UpdateSalesAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SalesAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Sales adjustment not found.");
        var invoice = request.RelatedInvoiceId.HasValue
            ? await _dbContext.Invoices.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == request.RelatedInvoiceId.Value, cancellationToken)
            : null;
        var customer = request.CustomerId.HasValue
            ? await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken)
            : invoice?.Customer;

        var currency = NormalizeCurrency(request.Currency, entity.Currency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(Math.Abs(request.AmountOriginal));

        entity.AdjustmentType = request.AdjustmentType;
        entity.TransactionDate = request.TransactionDate;
        entity.RelatedInvoiceId = invoice?.Id;
        entity.RelatedInvoiceNumber = invoice?.InvoiceNo;
        entity.CustomerId = customer?.Id;
        entity.CustomerName = customer?.Name;
        entity.Currency = currency;
        entity.ExchangeRate = exchangeRate;
        entity.AmountOriginal = amountOriginal;
        entity.AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate);
        entity.ApprovalStatus = request.ApprovalStatus;
        entity.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.SalesAdjustmentUpdated,
            nameof(SalesAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            new { entity.AdjustmentType, entity.AmountMvr, entity.Currency },
            cancellationToken);

        return MapSalesAdjustment(entity);
    }

    public async Task DeleteSalesAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SalesAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Sales adjustment not found.");

        _dbContext.SalesAdjustments.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.SalesAdjustmentDeleted,
            nameof(SalesAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<OtherIncomeEntryDto>> GetOtherIncomeEntriesAsync(OtherIncomeEntryListQuery query, CancellationToken cancellationToken = default)
    {
        var entries = _dbContext.OtherIncomeEntries.AsNoTracking();

        if (query.DateFrom.HasValue)
        {
            entries = entries.Where(x => x.TransactionDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            entries = entries.Where(x => x.TransactionDate <= query.DateTo.Value);
        }

        if (query.ApprovalStatus.HasValue)
        {
            entries = entries.Where(x => x.ApprovalStatus == query.ApprovalStatus.Value);
        }

        var items = await entries
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapOtherIncomeEntry).ToList();
    }

    public async Task<OtherIncomeEntryDto> CreateOtherIncomeEntryAsync(CreateOtherIncomeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var customer = request.CustomerId.HasValue
            ? await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken)
            : null;
        var currency = NormalizeCurrency(request.Currency, (await GetTenantSettingsAsync(cancellationToken)).DefaultCurrency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(request.AmountOriginal);

        var entity = new OtherIncomeEntry
        {
            EntryNumber = await _documentNumberService.GenerateAsync(DocumentType.OtherIncome, request.TransactionDate, cancellationToken),
            TransactionDate = request.TransactionDate,
            CustomerId = customer?.Id,
            CounterpartyName = !string.IsNullOrWhiteSpace(request.CounterpartyName) ? request.CounterpartyName.Trim() : customer?.Name,
            Description = request.Description.Trim(),
            Currency = currency,
            ExchangeRate = exchangeRate,
            AmountOriginal = amountOriginal,
            AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate),
            ApprovalStatus = request.ApprovalStatus,
            Notes = request.Notes?.Trim()
        };

        _dbContext.OtherIncomeEntries.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.OtherIncomeEntryCreated,
            nameof(OtherIncomeEntry),
            entity.Id.ToString(),
            entity.EntryNumber,
            new { entity.AmountMvr, entity.Currency },
            cancellationToken);

        return MapOtherIncomeEntry(entity);
    }

    public async Task<OtherIncomeEntryDto> UpdateOtherIncomeEntryAsync(Guid id, UpdateOtherIncomeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OtherIncomeEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Other income entry not found.");
        var customer = request.CustomerId.HasValue
            ? await _dbContext.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken)
            : null;
        var currency = NormalizeCurrency(request.Currency, entity.Currency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(request.AmountOriginal);

        entity.TransactionDate = request.TransactionDate;
        entity.CustomerId = customer?.Id;
        entity.CounterpartyName = !string.IsNullOrWhiteSpace(request.CounterpartyName) ? request.CounterpartyName.Trim() : customer?.Name;
        entity.Description = request.Description.Trim();
        entity.Currency = currency;
        entity.ExchangeRate = exchangeRate;
        entity.AmountOriginal = amountOriginal;
        entity.AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate);
        entity.ApprovalStatus = request.ApprovalStatus;
        entity.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.OtherIncomeEntryUpdated,
            nameof(OtherIncomeEntry),
            entity.Id.ToString(),
            entity.EntryNumber,
            new { entity.AmountMvr, entity.Currency },
            cancellationToken);

        return MapOtherIncomeEntry(entity);
    }

    public async Task DeleteOtherIncomeEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.OtherIncomeEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Other income entry not found.");

        _dbContext.OtherIncomeEntries.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.OtherIncomeEntryDeleted,
            nameof(OtherIncomeEntry),
            entity.Id.ToString(),
            entity.EntryNumber,
            null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<BptAdjustmentDto>> GetAdjustmentsAsync(BptAdjustmentListQuery query, CancellationToken cancellationToken = default)
    {
        var adjustments = _dbContext.BptAdjustments
            .AsNoTracking()
            .Include(x => x.BptCategory)
            .AsQueryable();

        if (query.DateFrom.HasValue)
        {
            adjustments = adjustments.Where(x => x.TransactionDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            adjustments = adjustments.Where(x => x.TransactionDate <= query.DateTo.Value);
        }

        if (query.BptCategoryId.HasValue)
        {
            adjustments = adjustments.Where(x => x.BptCategoryId == query.BptCategoryId.Value);
        }

        if (query.ApprovalStatus.HasValue)
        {
            adjustments = adjustments.Where(x => x.ApprovalStatus == query.ApprovalStatus.Value);
        }

        var items = await adjustments
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(MapAdjustment).ToList();
    }

    public async Task<BptAdjustmentDto> CreateAdjustmentAsync(CreateBptAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var bptCategory = await _dbContext.BptCategories.FirstOrDefaultAsync(x => x.Id == request.BptCategoryId, cancellationToken)
            ?? throw new NotFoundException("BPT category not found.");
        var currency = NormalizeCurrency(request.Currency, (await GetTenantSettingsAsync(cancellationToken)).DefaultCurrency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(request.AmountOriginal);

        var entity = new BptAdjustment
        {
            AdjustmentNumber = await _documentNumberService.GenerateAsync(DocumentType.BptAdjustment, request.TransactionDate, cancellationToken),
            TransactionDate = request.TransactionDate,
            Description = request.Description.Trim(),
            BptCategoryId = bptCategory.Id,
            Currency = currency,
            ExchangeRate = exchangeRate,
            AmountOriginal = amountOriginal,
            AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate),
            ApprovalStatus = request.ApprovalStatus,
            Notes = request.Notes?.Trim()
        };

        _dbContext.BptAdjustments.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.BptAdjustmentCreated,
            nameof(BptAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            new { bptCategory.Name, entity.AmountMvr, entity.Currency },
            cancellationToken);

        entity.BptCategory = bptCategory;
        return MapAdjustment(entity);
    }

    public async Task<BptAdjustmentDto> UpdateAdjustmentAsync(Guid id, UpdateBptAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BptAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("BPT adjustment not found.");
        var bptCategory = await _dbContext.BptCategories.FirstOrDefaultAsync(x => x.Id == request.BptCategoryId, cancellationToken)
            ?? throw new NotFoundException("BPT category not found.");
        var currency = NormalizeCurrency(request.Currency, entity.Currency);
        var exchangeRate = await ResolveStoredOrExplicitRateAsync(request.TransactionDate, currency, request.ExchangeRate, cancellationToken);
        var amountOriginal = Round2(request.AmountOriginal);

        entity.TransactionDate = request.TransactionDate;
        entity.Description = request.Description.Trim();
        entity.BptCategoryId = bptCategory.Id;
        entity.Currency = currency;
        entity.ExchangeRate = exchangeRate;
        entity.AmountOriginal = amountOriginal;
        entity.AmountMvr = ConvertAmount(amountOriginal, currency, exchangeRate);
        entity.ApprovalStatus = request.ApprovalStatus;
        entity.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.BptAdjustmentUpdated,
            nameof(BptAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            new { bptCategory.Name, entity.AmountMvr, entity.Currency },
            cancellationToken);

        entity.BptCategory = bptCategory;
        return MapAdjustment(entity);
    }

    public async Task DeleteAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.BptAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("BPT adjustment not found.");

        _dbContext.BptAdjustments.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.BptAdjustmentDeleted,
            nameof(BptAdjustment),
            entity.Id.ToString(),
            entity.AdjustmentNumber,
            null,
            cancellationToken);
    }

    private async Task EnsureDefaultBptSetupAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");

        if (!await _dbContext.ExpenseCategories.AnyAsync(cancellationToken))
        {
            _dbContext.ExpenseCategories.AddRange(ExpenseCategoryService.BuildDefaultCategories(tenantId));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var categories = await _dbContext.BptCategories.ToListAsync(cancellationToken);
        var desiredCategories = BuildDefaultBptCategories(tenantId);

        foreach (var desired in desiredCategories)
        {
            if (categories.Any(x => x.Code == desired.Code))
            {
                continue;
            }

            _dbContext.BptCategories.Add(desired);
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var currentCategories = await _dbContext.BptCategories.ToListAsync(cancellationToken);
        var categoryByCode = currentCategories.ToDictionary(x => x.Code);
        var expenseCategories = await _dbContext.ExpenseCategories.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var existingMappings = await _dbContext.BptMappingRules.ToListAsync(cancellationToken);

        foreach (var expenseCategory in expenseCategories)
        {
            if (existingMappings.Any(x => x.ExpenseCategoryId == expenseCategory.Id && x.SourceModule == null))
            {
                continue;
            }

            _dbContext.BptMappingRules.Add(new BptMappingRule
            {
                Name = $"{expenseCategory.Name} default mapping",
                ExpenseCategoryId = expenseCategory.Id,
                BptCategoryId = categoryByCode[ResolveDefaultExpenseMappingCode(expenseCategory.BptCategoryCode)].Id,
                Priority = 100,
                IsSystem = true,
                IsActive = true
            });
        }

        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Invoice revenue", BptSourceModule.Invoice, BptCategoryCode.GrossSales, priority: 10);
        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Sales return", BptSourceModule.SalesAdjustment, BptCategoryCode.SalesReturnsAndAllowances, priority: 10, adjustmentType: SalesAdjustmentType.Return);
        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Sales allowance", BptSourceModule.SalesAdjustment, BptCategoryCode.SalesReturnsAndAllowances, priority: 10, adjustmentType: SalesAdjustmentType.Allowance);
        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Other income", BptSourceModule.OtherIncome, BptCategoryCode.OtherIncome, priority: 10);
        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Payroll salary", BptSourceModule.Payroll, BptCategoryCode.Salary, priority: 10);
        AddSystemRuleIfMissing(existingMappings, categoryByCode, "Capital purchase exclusion", BptSourceModule.ReceivedInvoice, BptCategoryCode.NonOperatingExcluded, priority: 5, revenueCapitalClassification: RevenueCapitalType.Capital);

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static BptRangeContext ResolveRange(BptReportQuery query)
    {
        return query.PeriodMode switch
        {
            BptPeriodMode.Quarter => ResolveQuarterRange(query.Year, query.Quarter),
            BptPeriodMode.Year => new BptRangeContext(new DateOnly(query.Year, 1, 1), new DateOnly(query.Year, 12, 31), query.Year.ToString(CultureInfo.InvariantCulture)),
            BptPeriodMode.CustomRange => ResolveCustomRange(query.CustomStartDate, query.CustomEndDate),
            _ => throw new AppException("Unsupported BPT period mode.")
        };
    }

    private static BptRangeContext ResolveQuarterRange(int year, int? quarter)
    {
        var resolvedQuarter = quarter is >= 1 and <= 4 ? quarter.Value : 1;
        var startMonth = ((resolvedQuarter - 1) * 3) + 1;
        var start = new DateOnly(year, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return new BptRangeContext(start, end, $"Q{resolvedQuarter} {year}");
    }

    private static BptRangeContext ResolveCustomRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            throw new AppException("Custom date range requires both start and end dates.");
        }

        if (endDate.Value < startDate.Value)
        {
            throw new AppException("End date cannot be earlier than start date.");
        }

        return new BptRangeContext(startDate.Value, endDate.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
    }

    private async Task<List<BptTraceTransactionDto>> BuildInvoiceTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        IReadOnlyList<ExchangeRate> exchangeRates,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var mapping = ResolveMappingRule(mappings, BptSourceModule.Invoice)
            ?? throw new AppException("BPT invoice mapping is missing.");

        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.DateIssued >= range.StartDate && x.DateIssued <= range.EndDate)
            .OrderBy(x => x.DateIssued)
            .ThenBy(x => x.InvoiceNo)
            .ToListAsync(cancellationToken);

        var rows = new List<BptTraceTransactionDto>(invoices.Count);
        foreach (var invoice in invoices)
        {
            var currency = NormalizeCurrency(invoice.Currency, CurrencyCode.MVR.ToString());
            var exchangeRate = ResolveRateOrTrackMissing(invoice.DateIssued, currency, exchangeRates, context);
            if (exchangeRate <= 0)
            {
                continue;
            }

            var amountOriginal = Round2(invoice.Subtotal);
            rows.Add(BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.Invoice,
                invoice.Id,
                invoice.InvoiceNo,
                invoice.DateIssued,
                invoice.Customer.Name,
                $"Invoice issued to {invoice.Customer.Name}",
                currency,
                exchangeRate,
                amountOriginal,
                ConvertAmount(amountOriginal, currency, exchangeRate),
                invoice.PaymentStatus.ToString(),
                false,
                invoice.Notes));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildSalesAdjustmentTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var adjustments = await _dbContext.SalesAdjustments
            .AsNoTracking()
            .Where(x => x.TransactionDate >= range.StartDate
                && x.TransactionDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.AdjustmentNumber)
            .ToListAsync(cancellationToken);

        var rows = new List<BptTraceTransactionDto>(adjustments.Count);
        foreach (var adjustment in adjustments)
        {
            var mapping = ResolveMappingRule(mappings, BptSourceModule.SalesAdjustment, null, adjustment.AdjustmentType)
                ?? throw new AppException($"BPT mapping is missing for sales adjustment type {adjustment.AdjustmentType}.");

            rows.Add(BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.SalesAdjustment,
                adjustment.Id,
                adjustment.AdjustmentNumber,
                adjustment.TransactionDate,
                Safe(adjustment.CustomerName),
                $"{adjustment.AdjustmentType} {Safe(adjustment.RelatedInvoiceNumber)}".Trim(),
                NormalizeCurrency(adjustment.Currency, CurrencyCode.MVR.ToString()),
                adjustment.ExchangeRate,
                Round2(adjustment.AmountOriginal),
                Round2(adjustment.AmountMvr),
                adjustment.ApprovalStatus.ToString(),
                false,
                adjustment.Notes));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildOtherIncomeTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var mapping = ResolveMappingRule(mappings, BptSourceModule.OtherIncome)
            ?? throw new AppException("BPT mapping is missing for other income.");

        var entries = await _dbContext.OtherIncomeEntries
            .AsNoTracking()
            .Where(x => x.TransactionDate >= range.StartDate
                && x.TransactionDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.EntryNumber)
            .ToListAsync(cancellationToken);

        return entries
            .Select(entry => BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.OtherIncome,
                entry.Id,
                entry.EntryNumber,
                entry.TransactionDate,
                Safe(entry.CounterpartyName),
                entry.Description,
                NormalizeCurrency(entry.Currency, CurrencyCode.MVR.ToString()),
                entry.ExchangeRate,
                Round2(entry.AmountOriginal),
                Round2(entry.AmountMvr),
                entry.ApprovalStatus.ToString(),
                false,
                entry.Notes))
            .ToList();
    }

    private async Task<List<BptTraceTransactionDto>> BuildReceivedInvoiceTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        IReadOnlyList<ExchangeRate> exchangeRates,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var invoices = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var rows = new List<BptTraceTransactionDto>(invoices.Count);
        foreach (var invoice in invoices)
        {
            var mapping = ResolveMappingRule(mappings, BptSourceModule.ReceivedInvoice, invoice.ExpenseCategoryId, null, invoice.RevenueCapitalClassification)
                ?? ResolveMappingRule(mappings, BptSourceModule.ReceivedInvoice, invoice.ExpenseCategoryId)
                ?? throw new AppException($"BPT mapping is missing for expense category {invoice.ExpenseCategory.Name}.");

            if (invoice.RevenueCapitalClassification == RevenueCapitalType.Capital
                && mapping.BptCategory.ClassificationGroup == BptClassificationGroup.Excluded)
            {
                context.ExcludedCapitalTransactions += 1;
            }

            var currency = NormalizeCurrency(invoice.Currency, CurrencyCode.MVR.ToString());
            var exchangeRate = ResolveRateOrTrackMissing(invoice.InvoiceDate, currency, exchangeRates, context);
            if (exchangeRate <= 0)
            {
                continue;
            }

            var amountOriginal = Round2(invoice.IsTaxClaimable ? invoice.TotalAmount - invoice.GstAmount : invoice.TotalAmount);
            rows.Add(BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.ReceivedInvoice,
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.SupplierName,
                invoice.Description ?? invoice.ExpenseCategory.Name,
                currency,
                exchangeRate,
                amountOriginal,
                ConvertAmount(amountOriginal, currency, exchangeRate),
                invoice.ApprovalStatus.ToString(),
                false,
                invoice.Notes));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildRentTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        IReadOnlyList<ExchangeRate> exchangeRates,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var entries = await _dbContext.RentEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.Date >= range.StartDate
                && x.Date <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.RentNumber)
            .ToListAsync(cancellationToken);

        var rows = new List<BptTraceTransactionDto>(entries.Count);
        foreach (var entry in entries)
        {
            var mapping = ResolveMappingRule(mappings, BptSourceModule.RentEntry, entry.ExpenseCategoryId)
                ?? throw new AppException($"BPT mapping is missing for rent expense category {entry.ExpenseCategory.Name}.");
            var currency = NormalizeCurrency(entry.Currency, CurrencyCode.MVR.ToString());
            var exchangeRate = ResolveRateOrTrackMissing(entry.Date, currency, exchangeRates, context);
            if (exchangeRate <= 0)
            {
                continue;
            }

            var amountOriginal = Round2(entry.Amount);
            rows.Add(BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.RentEntry,
                entry.Id,
                entry.RentNumber,
                entry.Date,
                entry.PayTo,
                entry.PropertyName,
                currency,
                exchangeRate,
                amountOriginal,
                ConvertAmount(amountOriginal, currency, exchangeRate),
                entry.ApprovalStatus.ToString(),
                false,
                entry.Notes));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildManualExpenseTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        IReadOnlyList<ExchangeRate> exchangeRates,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var entries = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.SourceType == ExpenseSourceType.Manual
                && x.TransactionDate >= range.StartDate
                && x.TransactionDate <= range.EndDate)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.DocumentNumber)
            .ToListAsync(cancellationToken);

        var rows = new List<BptTraceTransactionDto>(entries.Count);
        foreach (var entry in entries)
        {
            var mapping = ResolveMappingRule(mappings, BptSourceModule.ExpenseEntry, entry.ExpenseCategoryId)
                ?? throw new AppException($"BPT mapping is missing for expense category {entry.ExpenseCategory.Name}.");
            var currency = NormalizeCurrency(entry.Currency, CurrencyCode.MVR.ToString());
            var exchangeRate = ResolveRateOrTrackMissing(entry.TransactionDate, currency, exchangeRates, context);
            if (exchangeRate <= 0)
            {
                continue;
            }

            var amountOriginal = Round2(entry.GrossAmount - entry.ClaimableTaxAmount);
            rows.Add(BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.ExpenseEntry,
                entry.Id,
                entry.DocumentNumber,
                entry.TransactionDate,
                entry.PayeeName,
                entry.Description ?? entry.ExpenseCategory.Name,
                currency,
                exchangeRate,
                amountOriginal,
                ConvertAmount(amountOriginal, currency, exchangeRate),
                ExpenseSourceType.Manual.ToString(),
                false,
                entry.Notes));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildPayrollTransactionsAsync(
        BptRangeContext range,
        IReadOnlyList<BptMappingRule> mappings,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var mapping = ResolveMappingRule(mappings, BptSourceModule.Payroll)
            ?? throw new AppException("BPT payroll mapping is missing.");

        var periods = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Include(x => x.Entries)
                .ThenInclude(x => x.Staff)
            .Where(x => x.EndDate >= range.StartDate && x.EndDate <= range.EndDate)
            .OrderBy(x => x.EndDate)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var finalizedPeriods = periods.Where(x => x.Status == PayrollPeriodStatus.Finalized).ToList();
        var scopedPeriods = finalizedPeriods.Count > 0 ? finalizedPeriods : periods;
        context.UseDraftPayrollPeriods = finalizedPeriods.Count == 0 && scopedPeriods.Count > 0;

        var rows = new List<BptTraceTransactionDto>();
        foreach (var period in scopedPeriods)
        {
            rows.AddRange(period.Entries.Select(entry => BuildTransaction(
                mapping.BptCategory,
                BptSourceModule.Payroll,
                entry.Id,
                $"{period.Year}-{period.Month:00}-{entry.Staff.StaffId}",
                period.EndDate,
                entry.Staff.StaffName,
                $"Payroll period {period.Year}-{period.Month:00}",
                CurrencyCode.MVR.ToString(),
                1m,
                Round2(entry.TotalPay),
                Round2(entry.TotalPay),
                period.Status.ToString(),
                false,
                null)));
        }

        return rows;
    }

    private async Task<List<BptTraceTransactionDto>> BuildAdjustmentTransactionsAsync(
        BptRangeContext range,
        BptBuildContext context,
        CancellationToken cancellationToken)
    {
        var adjustments = await _dbContext.BptAdjustments
            .AsNoTracking()
            .Include(x => x.BptCategory)
            .Where(x => x.TransactionDate >= range.StartDate
                && x.TransactionDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(x => x.TransactionDate)
            .ThenBy(x => x.AdjustmentNumber)
            .ToListAsync(cancellationToken);

        context.ManualAdjustmentCount = adjustments.Count;

        return adjustments
            .Select(adjustment => BuildTransaction(
                adjustment.BptCategory,
                BptSourceModule.BptAdjustment,
                adjustment.Id,
                adjustment.AdjustmentNumber,
                adjustment.TransactionDate,
                "Adjustment",
                adjustment.Description,
                NormalizeCurrency(adjustment.Currency, CurrencyCode.MVR.ToString()),
                adjustment.ExchangeRate,
                Round2(adjustment.AmountOriginal),
                Round2(adjustment.AmountMvr),
                adjustment.ApprovalStatus.ToString(),
                true,
                adjustment.Notes))
            .ToList();
    }

    private async Task<List<BptSalaryNoteRowDto>> BuildSalaryNotesAsync(BptRangeContext range, bool useDraftPeriods, CancellationToken cancellationToken)
    {
        var entries = _dbContext.PayrollEntries
            .AsNoTracking()
            .Include(x => x.Staff)
            .Include(x => x.PayrollPeriod)
            .Where(x => x.PayrollPeriod.EndDate >= range.StartDate && x.PayrollPeriod.EndDate <= range.EndDate);

        if (!useDraftPeriods)
        {
            entries = entries.Where(x => x.PayrollPeriod.Status == PayrollPeriodStatus.Finalized);
        }

        var payrollEntries = await entries
            .OrderBy(x => x.Staff.StaffName)
            .ToListAsync(cancellationToken);

        return payrollEntries
            .GroupBy(x => new { x.Staff.StaffId, x.Staff.StaffName })
            .Select(group => new BptSalaryNoteRowDto
            {
                StaffCode = group.Key.StaffId,
                StaffName = group.Key.StaffName,
                AverageBasicPerPeriod = Round2(group.Average(x => x.Basic)),
                AverageAllowancePerPeriod = Round2(group.Average(x => x.ServiceAllowance + x.OtherAllowance + x.PhoneAllowance + x.FoodAllowance + x.OvertimePay)),
                PeriodCount = group.Select(x => x.PayrollPeriodId).Distinct().Count(),
                TotalForPeriodRange = Round2(group.Sum(x => x.TotalPay))
            })
            .OrderBy(x => x.StaffName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BptIncomeStatementDto BuildStatement(IReadOnlyList<BptTraceTransactionDto> transactions)
    {
        var included = transactions.Where(x => x.ClassificationGroup != BptClassificationGroup.Excluded).ToList();

        var grossSales = Round2(included
            .Where(x => x.ClassificationGroup == BptClassificationGroup.Revenue)
            .Sum(x => x.AmountMvr));
        var salesReturns = Round2(included
            .Where(x => x.ClassificationGroup == BptClassificationGroup.SalesReturnAllowance)
            .Sum(x => x.AmountMvr));
        var netSales = Round2(grossSales - salesReturns);

        var costOfGoodsSoldLines = BuildIncomeLines(included, BptClassificationGroup.CostOfGoodsSold);
        var costOfGoodsSold = Round2(costOfGoodsSoldLines.Sum(x => x.Amount));
        var grossProfit = Round2(netSales - costOfGoodsSold);

        var operatingExpenseLines = BuildIncomeLines(included, BptClassificationGroup.OperatingExpense);
        var totalOperatingExpenses = Round2(operatingExpenseLines.Sum(x => x.Amount));
        var netOperatingIncome = Round2(grossProfit - totalOperatingExpenses);

        var otherIncomeLines = BuildIncomeLines(included, BptClassificationGroup.OtherIncome);
        var totalOtherIncome = Round2(otherIncomeLines.Sum(x => x.Amount));

        return new BptIncomeStatementDto
        {
            GrossSales = grossSales,
            SalesReturnsAndAllowances = salesReturns,
            NetSales = netSales,
            CostOfGoodsSoldLines = costOfGoodsSoldLines,
            CostOfGoodsSold = costOfGoodsSold,
            GrossProfit = grossProfit,
            OperatingExpenses = operatingExpenseLines,
            TotalOperatingExpenses = totalOperatingExpenses,
            NetOperatingIncome = netOperatingIncome,
            OtherIncome = otherIncomeLines,
            TotalOtherIncome = totalOtherIncome,
            NetIncome = Round2(netOperatingIncome + totalOtherIncome)
        };
    }

    private static List<BptExpenseNoteSectionDto> BuildNoteSections(IReadOnlyList<BptTraceTransactionDto> transactions)
    {
        return transactions
            .Where(x => x.ClassificationGroup != BptClassificationGroup.Revenue
                && x.ClassificationGroup != BptClassificationGroup.Excluded
                && x.BptCategoryCode != BptCategoryCode.Salary)
            .GroupBy(x => new { x.BptCategoryCode, x.BptCategoryName })
            .OrderBy(x => x.Key.BptCategoryName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BptExpenseNoteSectionDto
            {
                CategoryCode = group.Key.BptCategoryCode,
                Title = group.Key.BptCategoryName,
                TotalAmount = Round2(group.Sum(x => x.AmountMvr)),
                Rows = group
                    .OrderBy(x => x.TransactionDate)
                    .ThenBy(x => x.SourceDocumentNumber, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new BptExpenseNoteRowDto
                    {
                        Date = x.TransactionDate,
                        DocumentNumber = x.SourceDocumentNumber,
                        PayeeName = x.CounterpartyName,
                        Detail = x.Description,
                        Amount = Round2(x.AmountMvr)
                    })
                    .ToList()
            })
            .ToList();
    }

    private static List<string> BuildImportantNotes(BptBuildContext context, IReadOnlyList<BptTraceTransactionDto> transactions)
    {
        var notes = new List<string>();

        if (transactions.Any(x => !IsMvr(x.Currency)))
        {
            notes.Add("Foreign-currency transactions were converted to MVR using stored exchange rates.");
        }

        if (context.ExcludedCapitalTransactions > 0)
        {
            notes.Add($"{context.ExcludedCapitalTransactions:N0} capital-classified purchase record(s) were excluded from BPT totals.");
        }

        if (context.ManualAdjustmentCount > 0)
        {
            notes.Add($"{context.ManualAdjustmentCount:N0} manual BPT adjustment(s) are included in this filing period.");
        }

        if (context.UseDraftPayrollPeriods)
        {
            notes.Add("Draft payroll periods were included because no finalized payroll periods exist in the selected range.");
        }

        return notes;
    }

    private static IReadOnlyList<BptCategory> BuildDefaultBptCategories(Guid tenantId)
    {
        return new List<BptCategory>
        {
            CreateBptCategory(tenantId, BptCategoryCode.GrossSales, "Gross Sales", BptClassificationGroup.Revenue, 10),
            CreateBptCategory(tenantId, BptCategoryCode.SalesReturnsAndAllowances, "Sales Returns and Allowances", BptClassificationGroup.SalesReturnAllowance, 20),
            CreateBptCategory(tenantId, BptCategoryCode.OtherIncome, "Other Income", BptClassificationGroup.OtherIncome, 30),
            CreateBptCategory(tenantId, BptCategoryCode.CostOfGoodsSold, "Cost of Goods Sold", BptClassificationGroup.CostOfGoodsSold, 40),
            CreateBptCategory(tenantId, BptCategoryCode.Salary, "Salary", BptClassificationGroup.OperatingExpense, 100),
            CreateBptCategory(tenantId, BptCategoryCode.Rent, "Rent", BptClassificationGroup.OperatingExpense, 110),
            CreateBptCategory(tenantId, BptCategoryCode.RepairAndMaintenance, "Repair and Maintenance", BptClassificationGroup.OperatingExpense, 120),
            CreateBptCategory(tenantId, BptCategoryCode.OfficeExpense, "Office Expense", BptClassificationGroup.OperatingExpense, 130),
            CreateBptCategory(tenantId, BptCategoryCode.LicenseAndRegistrationFees, "License and Registration Fees", BptClassificationGroup.OperatingExpense, 140),
            CreateBptCategory(tenantId, BptCategoryCode.DieselCharges, "Diesel / Fuel", BptClassificationGroup.OperatingExpense, 150),
            CreateBptCategory(tenantId, BptCategoryCode.HiredFerryCharges, "Hired Ferry Charges", BptClassificationGroup.OperatingExpense, 160),
            CreateBptCategory(tenantId, BptCategoryCode.UtilitiesTelephoneInternet, "Utilities / Telephone / Internet", BptClassificationGroup.OperatingExpense, 170),
            CreateBptCategory(tenantId, BptCategoryCode.OfficeSupplies, "Office Supplies", BptClassificationGroup.OperatingExpense, 180),
            CreateBptCategory(tenantId, BptCategoryCode.Insurance, "Insurance", BptClassificationGroup.OperatingExpense, 190),
            CreateBptCategory(tenantId, BptCategoryCode.LegalAndProfessionalFees, "Legal and Professional Fees", BptClassificationGroup.OperatingExpense, 200),
            CreateBptCategory(tenantId, BptCategoryCode.Travel, "Travel", BptClassificationGroup.OperatingExpense, 210),
            CreateBptCategory(tenantId, BptCategoryCode.Other, "Other Operating Expenses", BptClassificationGroup.OperatingExpense, 220),
            CreateBptCategory(tenantId, BptCategoryCode.NonOperatingExcluded, "Excluded / Non-operating", BptClassificationGroup.Excluded, 900)
        };
    }

    private static BptCategory CreateBptCategory(Guid tenantId, BptCategoryCode code, string name, BptClassificationGroup group, int sortOrder)
    {
        return new BptCategory
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            ClassificationGroup = group,
            IsSystem = true,
            IsActive = true,
            SortOrder = sortOrder
        };
    }

    private void AddSystemRuleIfMissing(
        IReadOnlyCollection<BptMappingRule> existingMappings,
        IReadOnlyDictionary<BptCategoryCode, BptCategory> categoryByCode,
        string name,
        BptSourceModule sourceModule,
        BptCategoryCode code,
        int priority,
        SalesAdjustmentType? adjustmentType = null,
        RevenueCapitalType? revenueCapitalClassification = null)
    {
        if (existingMappings.Any(x =>
                x.SourceModule == sourceModule
                && x.ExpenseCategoryId == null
                && x.SalesAdjustmentType == adjustmentType
                && x.RevenueCapitalClassification == revenueCapitalClassification))
        {
            return;
        }

        _dbContext.BptMappingRules.Add(new BptMappingRule
        {
            Name = name,
            SourceModule = sourceModule,
            SalesAdjustmentType = adjustmentType,
            RevenueCapitalClassification = revenueCapitalClassification,
            BptCategoryId = categoryByCode[code].Id,
            Priority = priority,
            IsSystem = true,
            IsActive = true
        });
    }

    private static BptCategoryCode ResolveDefaultExpenseMappingCode(BptCategoryCode expenseCategoryCode)
    {
        return expenseCategoryCode switch
        {
            BptCategoryCode.CostOfGoodsSold => BptCategoryCode.CostOfGoodsSold,
            BptCategoryCode.NonOperatingExcluded => BptCategoryCode.NonOperatingExcluded,
            BptCategoryCode.Salary => BptCategoryCode.Salary,
            BptCategoryCode.Rent => BptCategoryCode.Rent,
            BptCategoryCode.LicenseAndRegistrationFees => BptCategoryCode.LicenseAndRegistrationFees,
            BptCategoryCode.OfficeExpense => BptCategoryCode.OfficeExpense,
            BptCategoryCode.RepairAndMaintenance => BptCategoryCode.RepairAndMaintenance,
            BptCategoryCode.DieselCharges => BptCategoryCode.DieselCharges,
            BptCategoryCode.HiredFerryCharges => BptCategoryCode.HiredFerryCharges,
            BptCategoryCode.UtilitiesTelephoneInternet => BptCategoryCode.UtilitiesTelephoneInternet,
            BptCategoryCode.OfficeSupplies => BptCategoryCode.OfficeSupplies,
            BptCategoryCode.Insurance => BptCategoryCode.Insurance,
            BptCategoryCode.LegalAndProfessionalFees => BptCategoryCode.LegalAndProfessionalFees,
            BptCategoryCode.Travel => BptCategoryCode.Travel,
            _ => BptCategoryCode.Other
        };
    }

    private static BptMappingRule? ResolveMappingRule(
        IEnumerable<BptMappingRule> mappings,
        BptSourceModule sourceModule,
        Guid? expenseCategoryId = null,
        SalesAdjustmentType? adjustmentType = null,
        RevenueCapitalType? revenueCapitalClassification = null)
    {
        return mappings
            .Where(x => x.IsActive)
            .Where(x => x.SourceModule == null || x.SourceModule == sourceModule)
            .Where(x => !x.ExpenseCategoryId.HasValue || x.ExpenseCategoryId == expenseCategoryId)
            .Where(x => !x.SalesAdjustmentType.HasValue || x.SalesAdjustmentType == adjustmentType)
            .Where(x => !x.RevenueCapitalClassification.HasValue || x.RevenueCapitalClassification == revenueCapitalClassification)
            .OrderBy(x => x.SourceModule == sourceModule ? 0 : 1)
            .ThenBy(x => x.ExpenseCategoryId.HasValue ? 0 : 1)
            .ThenBy(x => x.RevenueCapitalClassification.HasValue ? 0 : 1)
            .ThenBy(x => x.SalesAdjustmentType.HasValue ? 0 : 1)
            .ThenBy(x => x.Priority)
            .FirstOrDefault();
    }

    private static BptTraceTransactionDto BuildTransaction(
        BptCategory category,
        BptSourceModule sourceModule,
        Guid? sourceDocumentId,
        string sourceDocumentNumber,
        DateOnly transactionDate,
        string counterpartyName,
        string description,
        string currency,
        decimal exchangeRate,
        decimal amountOriginal,
        decimal amountMvr,
        string sourceStatus,
        bool isAdjustment,
        string? notes)
    {
        return new BptTraceTransactionDto
        {
            SourceDocumentId = sourceDocumentId,
            SourceModule = sourceModule,
            SourceDocumentNumber = sourceDocumentNumber,
            TransactionDate = transactionDate,
            FinancialYear = transactionDate.Year,
            CounterpartyName = Safe(counterpartyName),
            Description = Safe(description),
            Currency = NormalizeCurrency(currency, CurrencyCode.MVR.ToString()),
            ExchangeRate = RoundRate(exchangeRate),
            AmountOriginal = Round2(amountOriginal),
            AmountMvr = Round2(amountMvr),
            SourceStatus = Safe(sourceStatus),
            ClassificationGroup = category.ClassificationGroup,
            BptCategoryId = category.Id,
            BptCategoryCode = category.Code,
            BptCategoryName = category.Name,
            IsAdjustment = isAdjustment,
            Notes = notes
        };
    }

    private decimal ResolveRateOrTrackMissing(DateOnly transactionDate, string currency, IReadOnlyList<ExchangeRate> exchangeRates, BptBuildContext context)
    {
        if (IsMvr(currency))
        {
            return 1m;
        }

        var rate = exchangeRates
            .Where(x => string.Equals(x.Currency, currency, StringComparison.OrdinalIgnoreCase) && x.RateDate <= transactionDate)
            .OrderByDescending(x => x.RateDate)
            .Select(x => x.RateToMvr)
            .FirstOrDefault();

        if (rate <= 0)
        {
            context.MissingExchangeRateMessages.Add($"{currency} {transactionDate:yyyy-MM-dd}");
            return 0m;
        }

        return rate;
    }

    private async Task<decimal> ResolveStoredOrExplicitRateAsync(DateOnly transactionDate, string currency, decimal? explicitRate, CancellationToken cancellationToken)
    {
        if (IsMvr(currency))
        {
            return 1m;
        }

        if (explicitRate.HasValue && explicitRate.Value > 0)
        {
            return RoundRate(explicitRate.Value);
        }

        var storedRate = await _dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.IsActive && x.Currency == currency && x.RateDate <= transactionDate)
            .OrderByDescending(x => x.RateDate)
            .Select(x => x.RateToMvr)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedRate <= 0)
        {
            throw new AppException($"Exchange rate is missing for {currency} on {transactionDate:yyyy-MM-dd}.");
        }

        return RoundRate(storedRate);
    }

    private static List<BptIncomeLineDto> BuildIncomeLines(IEnumerable<BptTraceTransactionDto> transactions, BptClassificationGroup group)
    {
        return transactions
            .Where(x => x.ClassificationGroup == group)
            .GroupBy(x => x.BptCategoryName)
            .Select(grouping => new BptIncomeLineDto
            {
                Label = grouping.Key,
                Amount = Round2(grouping.Sum(x => x.AmountMvr))
            })
            .Where(x => x.Amount != 0m)
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BptExchangeRateDto MapExchangeRate(ExchangeRate rate)
    {
        return new BptExchangeRateDto
        {
            Id = rate.Id,
            RateDate = rate.RateDate,
            Currency = rate.Currency,
            RateToMvr = RoundRate(rate.RateToMvr),
            Source = rate.Source,
            Notes = rate.Notes,
            IsActive = rate.IsActive
        };
    }

    private static SalesAdjustmentDto MapSalesAdjustment(SalesAdjustment item)
    {
        return new SalesAdjustmentDto
        {
            Id = item.Id,
            AdjustmentNumber = item.AdjustmentNumber,
            AdjustmentType = item.AdjustmentType,
            TransactionDate = item.TransactionDate,
            RelatedInvoiceId = item.RelatedInvoiceId,
            RelatedInvoiceNumber = item.RelatedInvoiceNumber,
            CustomerId = item.CustomerId,
            CustomerName = item.CustomerName,
            Currency = item.Currency,
            ExchangeRate = RoundRate(item.ExchangeRate),
            AmountOriginal = Round2(item.AmountOriginal),
            AmountMvr = Round2(item.AmountMvr),
            ApprovalStatus = item.ApprovalStatus,
            Notes = item.Notes
        };
    }

    private static OtherIncomeEntryDto MapOtherIncomeEntry(OtherIncomeEntry item)
    {
        return new OtherIncomeEntryDto
        {
            Id = item.Id,
            EntryNumber = item.EntryNumber,
            TransactionDate = item.TransactionDate,
            CustomerId = item.CustomerId,
            CounterpartyName = item.CounterpartyName,
            Description = item.Description,
            Currency = item.Currency,
            ExchangeRate = RoundRate(item.ExchangeRate),
            AmountOriginal = Round2(item.AmountOriginal),
            AmountMvr = Round2(item.AmountMvr),
            ApprovalStatus = item.ApprovalStatus,
            Notes = item.Notes
        };
    }

    private static BptAdjustmentDto MapAdjustment(BptAdjustment item)
    {
        return new BptAdjustmentDto
        {
            Id = item.Id,
            AdjustmentNumber = item.AdjustmentNumber,
            TransactionDate = item.TransactionDate,
            Description = item.Description,
            BptCategoryId = item.BptCategoryId,
            BptCategoryName = item.BptCategory.Name,
            BptCategoryCode = item.BptCategory.Code,
            ClassificationGroup = item.BptCategory.ClassificationGroup,
            Currency = item.Currency,
            ExchangeRate = RoundRate(item.ExchangeRate),
            AmountOriginal = Round2(item.AmountOriginal),
            AmountMvr = Round2(item.AmountMvr),
            ApprovalStatus = item.ApprovalStatus,
            Notes = item.Notes
        };
    }

    private static decimal ConvertAmount(decimal amountOriginal, string currency, decimal exchangeRate)
    {
        return IsMvr(currency)
            ? Round2(amountOriginal)
            : Round2(amountOriginal * exchangeRate);
    }

    private static MiraReportPreviewDto MapToLegacyPreview(BptReportDto report)
    {
        return new MiraReportPreviewDto
        {
            ReportType = MiraReportType.BptIncomeStatement,
            Meta = new MiraReportMetaDto
            {
                Title = report.Meta.Title,
                PeriodLabel = report.Meta.PeriodLabel,
                RangeStart = report.Meta.RangeStart,
                RangeEnd = report.Meta.RangeEnd,
                GeneratedAtUtc = report.Meta.GeneratedAtUtc,
                TaxableActivityNumber = report.Meta.TaxableActivityNumber,
                CompanyName = report.Meta.CompanyName,
                CompanyTinNumber = report.Meta.CompanyTinNumber
            },
            Assumptions = report.ImportantNotes,
            BptIncomeStatement = report.Statement,
            BptNotes = report.Notes
        };
    }

    private static string BuildCompanyInfo(string tin, string? phone, string? email)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tin))
        {
            parts.Add($"TIN: {tin.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            parts.Add($"Phone: {phone.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            parts.Add($"Email: {email.Trim()}");
        }

        return string.Join(" | ", parts);
    }

    private static string BuildFileName(BptReportDto report, string extension)
    {
        var period = report.Meta.RangeStart == report.Meta.RangeEnd
            ? report.Meta.RangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{report.Meta.RangeStart:yyyy-MM-dd}_to_{report.Meta.RangeEnd:yyyy-MM-dd}";

        return $"bpt-statement_{period}.{extension}";
    }

    private static void BuildSummarySheet(XLWorkbook workbook, BptReportDto report)
    {
        var sheet = workbook.Worksheets.Add("BPT Statement");
        var row = 1;

        sheet.Cell(row, 1).Value = report.Meta.CompanyName;
        sheet.Range(row, 1, row, 6).Merge().Style.Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(XLColor.FromHtml("#243B6B"));
        row++;
        sheet.Cell(row, 1).Value = report.Meta.Title;
        sheet.Range(row, 1, row, 6).Merge().Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(XLColor.FromHtml("#5E72E4"));
        row++;
        sheet.Cell(row, 1).Value = $"TIN: {report.Meta.CompanyTinNumber} | Activity No.: {report.Meta.TaxableActivityNumber} | Period: {report.Meta.PeriodLabel}";
        sheet.Range(row, 1, row, 8).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#6E7F9B"));
        row += 2;

        row = WriteMetricStrip(sheet, row, new[]
        {
            ("Net Sales", report.Statement.NetSales.ToString("N2", CultureInfo.InvariantCulture)),
            ("Gross Profit", report.Statement.GrossProfit.ToString("N2", CultureInfo.InvariantCulture)),
            ("Operating Expenses", report.Statement.TotalOperatingExpenses.ToString("N2", CultureInfo.InvariantCulture)),
            ("Net Income", report.Statement.NetIncome.ToString("N2", CultureInfo.InvariantCulture))
        });
        row += 1;

        row = WriteSectionWithLines(sheet, row, "Income Statement", new[]
        {
            ("Gross sales", report.Statement.GrossSales, false),
            ("Sales returns and allowances", report.Statement.SalesReturnsAndAllowances, false),
            ("Net sales", report.Statement.NetSales, true),
            ("Cost of goods sold", report.Statement.CostOfGoodsSold, false),
            ("Gross profit", report.Statement.GrossProfit, true),
            ("Total operating expenses", report.Statement.TotalOperatingExpenses, false),
            ("Net operating income", report.Statement.NetOperatingIncome, true),
            ("Total other income", report.Statement.TotalOtherIncome, false),
            ("Net income", report.Statement.NetIncome, true)
        });

        row += 1;
        row = WriteBreakdownSection(sheet, row, "Cost of Goods Sold Breakdown", report.Statement.CostOfGoodsSoldLines);
        row += 1;
        row = WriteBreakdownSection(sheet, row, "Operating Expense Breakdown", report.Statement.OperatingExpenses);
        row += 1;
        row = WriteBreakdownSection(sheet, row, "Other Income Breakdown", report.Statement.OtherIncome);

        sheet.Columns().AdjustToContents();
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 38);
        sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 18);
    }

    private static void BuildTransactionsSheet(XLWorkbook workbook, BptReportDto report)
    {
        var sheet = workbook.Worksheets.Add("Transactions");
        var headers = new[]
        {
            "Date", "Module", "Document Number", "Counterparty", "Description", "Currency",
            "Exchange Rate", "Original Amount", "Amount (MVR)", "Group", "Category", "Status", "Adjustment"
        };
        WriteTableHeaders(sheet, 1, headers);
        var row = 2;

        foreach (var item in report.Transactions)
        {
            sheet.Cell(row, 1).Value = item.TransactionDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 2).Value = item.SourceModule.ToString();
            sheet.Cell(row, 3).Value = item.SourceDocumentNumber;
            sheet.Cell(row, 4).Value = item.CounterpartyName;
            sheet.Cell(row, 5).Value = item.Description;
            sheet.Cell(row, 6).Value = item.Currency;
            sheet.Cell(row, 7).Value = item.ExchangeRate;
            sheet.Cell(row, 8).Value = item.AmountOriginal;
            sheet.Cell(row, 9).Value = item.AmountMvr;
            sheet.Cell(row, 10).Value = item.ClassificationGroup.ToString();
            sheet.Cell(row, 11).Value = item.BptCategoryName;
            sheet.Cell(row, 12).Value = item.SourceStatus;
            sheet.Cell(row, 13).Value = item.IsAdjustment ? "Yes" : "No";
            row++;
        }

        StyleTable(sheet, 2, Math.Max(row - 1, 2), headers.Length, dateColumns: new[] { 1 }, numericColumns: new[] { 7, 8, 9 });
    }

    private static void BuildSalarySheet(XLWorkbook workbook, BptReportDto report)
    {
        var sheet = workbook.Worksheets.Add("Salary Notes");
        var headers = new[] { "Staff ID", "Staff Name", "Avg Basic / Period", "Avg Allowance / Period", "Periods", "Total Salary" };
        WriteTableHeaders(sheet, 1, headers);
        var row = 2;

        foreach (var item in report.Notes.SalaryRows)
        {
            sheet.Cell(row, 1).Value = item.StaffCode;
            sheet.Cell(row, 2).Value = item.StaffName;
            sheet.Cell(row, 3).Value = item.AverageBasicPerPeriod;
            sheet.Cell(row, 4).Value = item.AverageAllowancePerPeriod;
            sheet.Cell(row, 5).Value = item.PeriodCount;
            sheet.Cell(row, 6).Value = item.TotalForPeriodRange;
            row++;
        }

        sheet.Cell(row, 5).Value = "Total";
        sheet.Cell(row, 6).Value = report.Notes.TotalSalary;
        sheet.Range(row, 1, row, headers.Length).Style.Font.SetBold();
        sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F7FF");

        StyleTable(sheet, 2, Math.Max(row, 2), headers.Length, dateColumns: Array.Empty<int>(), numericColumns: new[] { 3, 4, 5, 6 });
    }

    private static int WriteMetricStrip(IXLWorksheet sheet, int row, IReadOnlyList<(string Label, string Value)> metrics)
    {
        for (var index = 0; index < metrics.Count; index++)
        {
            var startColumn = 1 + (index * 2);
            var range = sheet.Range(row, startColumn, row + 1, startColumn + 1);
            range.Merge();
            range.Style.Fill.BackgroundColor = XLColor.FromHtml(index % 2 == 0 ? "#EEF4FF" : "#F1FBF8");
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFDCF6");
            range.Style.Alignment.WrapText = true;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var richText = sheet.Cell(row, startColumn).GetRichText();
            richText.ClearText();
            richText.AddText(metrics[index].Label.ToUpperInvariant()).SetBold().SetFontColor(XLColor.FromHtml("#5D71A5")).SetFontSize(9);
            richText.AddText(Environment.NewLine);
            richText.AddText(metrics[index].Value).SetBold().SetFontColor(XLColor.FromHtml("#243B6B")).SetFontSize(13);
        }

        return row + 2;
    }

    private static int WriteSectionWithLines(IXLWorksheet sheet, int row, string title, IReadOnlyList<(string Label, decimal Amount, bool Emphasize)> lines)
    {
        sheet.Cell(row, 1).Value = title;
        sheet.Range(row, 1, row, 2).Merge();
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        sheet.Range(row, 1, row, 2).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#243B6B"));
        row++;

        foreach (var line in lines)
        {
            sheet.Cell(row, 1).Value = line.Label;
            sheet.Cell(row, 2).Value = line.Amount;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            if (line.Emphasize)
            {
                sheet.Range(row, 1, row, 2).Style.Font.SetBold();
                sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F6F9FF");
            }
            row++;
        }

        return row;
    }

    private static int WriteBreakdownSection(IXLWorksheet sheet, int row, string title, IReadOnlyList<BptIncomeLineDto> lines)
    {
        sheet.Cell(row, 1).Value = title;
        sheet.Range(row, 1, row, 2).Merge();
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        sheet.Range(row, 1, row, 2).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#243B6B"));
        row++;

        if (lines.Count == 0)
        {
            sheet.Cell(row, 1).Value = "No entries";
            sheet.Cell(row, 2).Value = 0m;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            return row + 1;
        }

        foreach (var line in lines)
        {
            sheet.Cell(row, 1).Value = line.Label;
            sheet.Cell(row, 2).Value = line.Amount;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        return row;
    }

    private static void WriteTableHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
            cell.Style.Font.FontColor = XLColor.FromHtml("#546E9E");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1DDF5");
        }
    }

    private static void StyleTable(IXLWorksheet sheet, int dataStartRow, int endRow, int totalColumns, IReadOnlyCollection<int> dateColumns, IReadOnlyCollection<int> numericColumns)
    {
        var usedRange = sheet.Range(1, 1, endRow, totalColumns);
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Font.FontName = "Calibri";
        usedRange.Style.Font.FontSize = 10;

        if (dataStartRow <= endRow)
        {
            var dataRange = sheet.Range(dataStartRow, 1, endRow, totalColumns);
            dataRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            dataRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#E4EBFB");
            foreach (var dataRow in dataRange.Rows())
            {
                if ((dataRow.RowNumber() - dataStartRow) % 2 == 1)
                {
                    dataRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFCFF");
                }
            }
        }

        foreach (var column in dateColumns)
        {
            sheet.Column(column).Style.DateFormat.Format = "yyyy-MM-dd";
        }

        foreach (var column in numericColumns)
        {
            sheet.Column(column).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(endRow, 1), totalColumns).SetAutoFilter();
        sheet.Columns().AdjustToContents();
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return Enum.TryParse<CurrencyCode>(currency, true, out var parsed)
            ? parsed.ToString().ToUpperInvariant()
            : fallback.ToUpperInvariant();
    }

    private static bool IsMvr(string? currency)
        => string.Equals(NormalizeCurrency(currency, CurrencyCode.MVR.ToString()), CurrencyCode.MVR.ToString(), StringComparison.OrdinalIgnoreCase);

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value)
        => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static string Safe(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private sealed class BptBuildContext
    {
        public HashSet<string> MissingExchangeRateMessages { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int ExcludedCapitalTransactions { get; set; }
        public int ManualAdjustmentCount { get; set; }
        public bool UseDraftPayrollPeriods { get; set; }
    }

    private sealed record BptRangeContext(DateOnly StartDate, DateOnly EndDate, string Label);
}
