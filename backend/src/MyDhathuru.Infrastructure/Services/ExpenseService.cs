using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly IBusinessAuditLogService _auditLogService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPdfExportService _pdfExportService;

    public ExpenseService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        IBusinessAuditLogService auditLogService,
        IPdfExportService pdfExportService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _auditLogService = auditLogService;
        _pdfExportService = pdfExportService;
    }

    public async Task<PagedResult<ExpenseLedgerRowDto>> GetLedgerAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);

        var rows = await BuildLedgerRowsAsync(query.DateFrom, query.DateTo, cancellationToken);
        rows = ApplyLedgerFilters(rows, query);

        var ordered = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderBy(x => x.TransactionDate).ThenBy(x => x.DocumentNumber).ToList()
            : rows.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.DocumentNumber).ToList();

        var totalCount = ordered.Count;
        var items = ordered
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new PagedResult<ExpenseLedgerRowDto>
        {
            Items = items,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ExpenseSummaryDto> GetSummaryAsync(ExpenseSummaryQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);
        var rows = await BuildLedgerRowsAsync(query.DateFrom, query.DateTo, cancellationToken);

        return new ExpenseSummaryDto
        {
            TotalNetAmount = rows.Sum(x => x.NetAmount),
            TotalTaxAmount = rows.Sum(x => x.TaxAmount),
            TotalGrossAmount = rows.Sum(x => x.GrossAmount),
            TotalPendingAmount = rows.Sum(x => x.PendingAmount),
            ByCategory = rows
                .GroupBy(x => x.ExpenseCategoryName)
                .OrderByDescending(x => x.Sum(row => row.GrossAmount))
                .Select(group => new ExpenseSummaryBucketDto
                {
                    Label = group.Key,
                    NetAmount = group.Sum(x => x.NetAmount),
                    TaxAmount = group.Sum(x => x.TaxAmount),
                    GrossAmount = group.Sum(x => x.GrossAmount)
                })
                .ToList(),
            ByMonth = rows
                .GroupBy(x => $"{x.TransactionDate:yyyy-MM}")
                .OrderBy(x => x.Key)
                .Select(group => new ExpenseSummaryBucketDto
                {
                    Label = group.Key,
                    NetAmount = group.Sum(x => x.NetAmount),
                    TaxAmount = group.Sum(x => x.TaxAmount),
                    GrossAmount = group.Sum(x => x.GrossAmount)
                })
                .ToList()
        };
    }

    public async Task<byte[]> GeneratePdfAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);
        var rows = await GetFilteredLedgerRowsAsync(query, cancellationToken);
        var summary = BuildSummary(rows);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = $"TIN: {settings.TinNumber} | Phone: {settings.CompanyPhone} | Email: {settings.CompanyEmail}";
        return _pdfExportService.BuildExpenseLedgerPdf(rows, summary, settings.CompanyName, companyInfo, settings.LogoUrl, query);
    }

    public async Task<byte[]> GenerateExcelAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);
        var rows = await GetFilteredLedgerRowsAsync(query, cancellationToken);
        var summary = BuildSummary(rows);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5));

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ExpenseLedger");
        const int totalColumns = 10;

        var heroRange = sheet.Range(1, 1, 3, totalColumns);
        heroRange.Merge();
        heroRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#18315D");
        heroRange.Style.Font.FontColor = XLColor.White;
        heroRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        heroRange.Style.Alignment.WrapText = true;

        var hero = sheet.Cell(1, 1).GetRichText();
        hero.ClearText();
        hero.AddText("Expense Ledger").SetBold().SetFontSize(19).SetFontColor(XLColor.White);
        hero.AddText(Environment.NewLine);
        hero.AddText(settings.CompanyName).SetBold().SetFontSize(11).SetFontColor(XLColor.FromHtml("#DDE9FF"));
        hero.AddText(Environment.NewLine);
        hero.AddText($"Generated {generatedAt:yyyy-MM-dd HH:mm} MVT | {BuildFilterLabel(query)}")
            .SetFontSize(9)
            .SetFontColor(XLColor.FromHtml("#BFD4FF"));

        sheet.Row(1).Height = 26;
        sheet.Row(2).Height = 22;
        sheet.Row(3).Height = 22;

        WriteMetricTile(sheet, 5, 1, 2, "Total Net", FormatDualCurrency(rows, x => x.NetAmount));
        WriteMetricTile(sheet, 5, 3, 4, "Total Tax", FormatDualCurrency(rows, x => x.TaxAmount));
        WriteMetricTile(sheet, 5, 5, 6, "Total Gross", FormatDualCurrency(rows, x => x.GrossAmount));
        WriteMetricTile(sheet, 5, 7, 10, "Pending", FormatDualCurrency(rows, x => x.PendingAmount));

        sheet.Cell(8, 1).Value = "Date";
        sheet.Cell(8, 2).Value = "Document";
        sheet.Cell(8, 3).Value = "Source";
        sheet.Cell(8, 4).Value = "Category";
        sheet.Cell(8, 5).Value = "Payee";
        sheet.Cell(8, 6).Value = "Currency";
        sheet.Cell(8, 7).Value = "Net";
        sheet.Cell(8, 8).Value = "Tax";
        sheet.Cell(8, 9).Value = "Gross";
        sheet.Cell(8, 10).Value = "Pending";
        StyleExcelTableHeader(sheet.Range(8, 1, 8, totalColumns));

        var row = 9;
        if (rows.Count == 0)
        {
            var emptyRange = sheet.Range(row, 1, row + 1, totalColumns);
            emptyRange.Merge();
            emptyRange.Value = "No expense ledger rows matched the export filter.";
            emptyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            emptyRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            emptyRange.Style.Font.FontColor = XLColor.FromHtml("#6078A7");
            emptyRange.Style.Font.Bold = true;
            emptyRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7FAFF");
            emptyRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            emptyRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
            row += 2;
        }
        else
        {
            foreach (var item in rows)
            {
                sheet.Cell(row, 1).Value = item.TransactionDate.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 2).Value = item.DocumentNumber;
                sheet.Cell(row, 3).Value = item.SourceType;
                sheet.Cell(row, 4).Value = item.ExpenseCategoryName;
                sheet.Cell(row, 5).Value = item.PayeeName;
                sheet.Cell(row, 6).Value = item.Currency;
                sheet.Cell(row, 7).Value = item.NetAmount;
                sheet.Cell(row, 8).Value = item.TaxAmount;
                sheet.Cell(row, 9).Value = item.GrossAmount;
                sheet.Cell(row, 10).Value = item.PendingAmount;
                row++;
            }

            StyleExcelBodyRows(sheet.Range(9, 1, row - 1, totalColumns));
            sheet.Range(8, 1, row - 1, totalColumns).SetAutoFilter();
        }

        var categoryStart = row + 2;
        sheet.Cell(categoryStart, 1).Value = "By Category";
        WriteSectionHeader(sheet.Range(categoryStart, 1, categoryStart, 4));
        sheet.Cell(categoryStart, 7).Value = "By Month";
        WriteSectionHeader(sheet.Range(categoryStart, 7, categoryStart, 10));

        var categoryHeaderRow = categoryStart + 1;
        sheet.Cell(categoryHeaderRow, 1).Value = "Category";
        sheet.Cell(categoryHeaderRow, 2).Value = "Net";
        sheet.Cell(categoryHeaderRow, 3).Value = "Tax";
        sheet.Cell(categoryHeaderRow, 4).Value = "Gross";
        StyleExcelTableHeader(sheet.Range(categoryHeaderRow, 1, categoryHeaderRow, 4));

        sheet.Cell(categoryHeaderRow, 7).Value = "Month";
        sheet.Cell(categoryHeaderRow, 8).Value = "Net";
        sheet.Cell(categoryHeaderRow, 9).Value = "Tax";
        sheet.Cell(categoryHeaderRow, 10).Value = "Gross";
        StyleExcelTableHeader(sheet.Range(categoryHeaderRow, 7, categoryHeaderRow, 10));

        var categoryRow = categoryStart + 2;
        foreach (var item in summary.ByCategory)
        {
            sheet.Cell(categoryRow, 1).Value = item.Label;
            sheet.Cell(categoryRow, 2).Value = item.NetAmount;
            sheet.Cell(categoryRow, 3).Value = item.TaxAmount;
            sheet.Cell(categoryRow, 4).Value = item.GrossAmount;
            categoryRow++;
        }

        var monthRow = categoryStart + 2;
        foreach (var item in summary.ByMonth)
        {
            sheet.Cell(monthRow, 7).Value = item.Label;
            sheet.Cell(monthRow, 8).Value = item.NetAmount;
            sheet.Cell(monthRow, 9).Value = item.TaxAmount;
            sheet.Cell(monthRow, 10).Value = item.GrossAmount;
            monthRow++;
        }

        sheet.Column(1).Style.DateFormat.Format = "yyyy-MM-dd";
        sheet.Columns(7, 10).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns(1, 10).AdjustToContents();
        sheet.SheetView.FreezeRows(8);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ExpenseEntryDetailDto?> GetManualEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Supplier)
            .Where(x => x.SourceType == ExpenseSourceType.Manual && x.Id == id)
            .Select(x => new ExpenseEntryDetailDto
            {
                Id = x.Id,
                DocumentNumber = x.DocumentNumber,
                TransactionDate = x.TransactionDate,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                PayeeName = x.PayeeName,
                Currency = x.Currency,
                NetAmount = x.NetAmount,
                TaxAmount = x.TaxAmount,
                GrossAmount = x.GrossAmount,
                ClaimableTaxAmount = x.ClaimableTaxAmount,
                PendingAmount = x.PendingAmount,
                Description = x.Description,
                Notes = x.Notes
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExpenseEntryDetailDto> CreateManualEntryAsync(CreateManualExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);

        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        if (request.SupplierId.HasValue)
        {
            var supplierExists = await _dbContext.Suppliers.AnyAsync(x => x.Id == request.SupplierId.Value, cancellationToken);
            if (!supplierExists)
            {
                throw new NotFoundException("Supplier not found.");
            }
        }

        var entry = new ExpenseEntry
        {
            SourceType = ExpenseSourceType.Manual,
            DocumentNumber = request.DocumentNumber.Trim(),
            TransactionDate = request.TransactionDate,
            ExpenseCategoryId = category.Id,
            SupplierId = request.SupplierId,
            PayeeName = request.PayeeName.Trim(),
            Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency),
            NetAmount = Round2(request.NetAmount),
            TaxAmount = Round2(request.TaxAmount),
            ClaimableTaxAmount = Round2(request.ClaimableTaxAmount),
            PendingAmount = Round2(request.PendingAmount),
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim()
        };

        entry.SourceId = entry.Id;
        entry.GrossAmount = Round2(entry.NetAmount + entry.TaxAmount);

        _dbContext.ExpenseEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseEntryCreated,
            nameof(ExpenseEntry),
            entry.Id.ToString(),
            entry.DocumentNumber,
            new { entry.PayeeName, entry.GrossAmount, entry.Currency },
            cancellationToken);

        return (await GetManualEntryByIdAsync(entry.Id, cancellationToken))!;
    }

    public async Task<ExpenseEntryDetailDto> UpdateManualEntryAsync(Guid id, UpdateManualExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesAsync(cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);

        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.SourceType == ExpenseSourceType.Manual && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense entry not found.");

        var category = await _dbContext.ExpenseCategories.FirstOrDefaultAsync(x => x.Id == request.ExpenseCategoryId, cancellationToken)
            ?? throw new NotFoundException("Expense category not found.");

        if (request.SupplierId.HasValue)
        {
            var supplierExists = await _dbContext.Suppliers.AnyAsync(x => x.Id == request.SupplierId.Value, cancellationToken);
            if (!supplierExists)
            {
                throw new NotFoundException("Supplier not found.");
            }
        }

        entry.DocumentNumber = request.DocumentNumber.Trim();
        entry.TransactionDate = request.TransactionDate;
        entry.ExpenseCategoryId = category.Id;
        entry.SupplierId = request.SupplierId;
        entry.PayeeName = request.PayeeName.Trim();
        entry.Currency = NormalizeCurrency(request.Currency, settings.DefaultCurrency);
        entry.NetAmount = Round2(request.NetAmount);
        entry.TaxAmount = Round2(request.TaxAmount);
        entry.GrossAmount = Round2(entry.NetAmount + entry.TaxAmount);
        entry.ClaimableTaxAmount = Round2(request.ClaimableTaxAmount);
        entry.PendingAmount = Round2(request.PendingAmount);
        entry.Description = request.Description?.Trim();
        entry.Notes = request.Notes?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseEntryUpdated,
            nameof(ExpenseEntry),
            entry.Id.ToString(),
            entry.DocumentNumber,
            new { entry.PayeeName, entry.GrossAmount, entry.Currency },
            cancellationToken);

        return (await GetManualEntryByIdAsync(entry.Id, cancellationToken))!;
    }

    public async Task DeleteManualEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.ExpenseEntries.FirstOrDefaultAsync(x => x.SourceType == ExpenseSourceType.Manual && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense entry not found.");

        var hasVoucher = await _dbContext.PaymentVouchers.AnyAsync(x => x.LinkedExpenseEntryId == id, cancellationToken);
        if (hasVoucher)
        {
            throw new AppException("Expense entry is linked to a payment voucher and cannot be deleted.");
        }

        _dbContext.ExpenseEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            BusinessAuditActionType.ExpenseEntryDeleted,
            nameof(ExpenseEntry),
            entry.Id.ToString(),
            entry.DocumentNumber,
            null,
            cancellationToken);
    }

    private async Task<List<ExpenseLedgerRowDto>> BuildLedgerRowsAsync(DateOnly? dateFrom, DateOnly? dateTo, CancellationToken cancellationToken)
    {
        var receivedInvoices = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => (dateFrom == null || x.InvoiceDate >= dateFrom) && (dateTo == null || x.InvoiceDate <= dateTo))
            .Select(x => new ExpenseLedgerRowDto
            {
                SourceType = ExpenseSourceType.ReceivedInvoice.ToString(),
                SourceId = x.Id,
                DocumentNumber = x.InvoiceNumber,
                TransactionDate = x.InvoiceDate,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                BptCategoryCode = x.ExpenseCategory.BptCategoryCode,
                SupplierId = x.SupplierId,
                SupplierName = x.SupplierName,
                PayeeName = x.SupplierName,
                Currency = x.Currency,
                NetAmount = x.TotalAmount - x.GstAmount,
                TaxAmount = x.GstAmount,
                GrossAmount = x.TotalAmount,
                ClaimableTaxAmount = x.IsTaxClaimable ? x.GstAmount : 0m,
                PendingAmount = x.BalanceDue,
                Description = x.Description,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        var rentEntries = await _dbContext.RentEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => (dateFrom == null || x.Date >= dateFrom) && (dateTo == null || x.Date <= dateTo))
            .Select(x => new ExpenseLedgerRowDto
            {
                SourceType = ExpenseSourceType.Rent.ToString(),
                SourceId = x.Id,
                DocumentNumber = x.RentNumber,
                TransactionDate = x.Date,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                BptCategoryCode = x.ExpenseCategory.BptCategoryCode,
                SupplierId = null,
                SupplierName = null,
                PayeeName = x.PayTo,
                Currency = x.Currency,
                NetAmount = x.Amount,
                TaxAmount = 0m,
                GrossAmount = x.Amount,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = x.PropertyName,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        var manualEntries = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Include(x => x.Supplier)
            .Where(x => x.SourceType == ExpenseSourceType.Manual && (dateFrom == null || x.TransactionDate >= dateFrom) && (dateTo == null || x.TransactionDate <= dateTo))
            .Select(x => new ExpenseLedgerRowDto
            {
                SourceType = ExpenseSourceType.Manual.ToString(),
                SourceId = x.Id,
                DocumentNumber = x.DocumentNumber,
                TransactionDate = x.TransactionDate,
                ExpenseCategoryId = x.ExpenseCategoryId,
                ExpenseCategoryName = x.ExpenseCategory.Name,
                BptCategoryCode = x.ExpenseCategory.BptCategoryCode,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier != null ? x.Supplier.Name : null,
                PayeeName = x.PayeeName,
                Currency = x.Currency,
                NetAmount = x.NetAmount,
                TaxAmount = x.TaxAmount,
                GrossAmount = x.GrossAmount,
                ClaimableTaxAmount = x.ClaimableTaxAmount,
                PendingAmount = x.PendingAmount,
                Description = x.Description,
                Notes = x.Notes
            })
            .ToListAsync(cancellationToken);

        var salaryCategory = await _dbContext.ExpenseCategories
            .AsNoTracking()
            .Where(x => x.Code == "SAL")
            .Select(x => new { x.Id, x.Name, x.BptCategoryCode })
            .FirstOrDefaultAsync(cancellationToken);

        var payrollRows = await _dbContext.PayrollPeriods
            .AsNoTracking()
            .Where(x => (dateFrom == null || x.EndDate >= dateFrom) && (dateTo == null || x.EndDate <= dateTo))
            .Select(x => new ExpenseLedgerRowDto
            {
                SourceType = ExpenseSourceType.Payroll.ToString(),
                SourceId = x.Id,
                DocumentNumber = $"PAY-{x.Year}-{x.Month:00}",
                TransactionDate = x.EndDate,
                ExpenseCategoryId = salaryCategory != null ? salaryCategory.Id : null,
                ExpenseCategoryName = salaryCategory != null ? salaryCategory.Name : "Salary",
                BptCategoryCode = salaryCategory != null ? salaryCategory.BptCategoryCode : BptCategoryCode.Salary,
                SupplierId = null,
                SupplierName = null,
                PayeeName = "Payroll",
                Currency = "MVR",
                NetAmount = x.TotalNetPayable,
                TaxAmount = 0m,
                GrossAmount = x.TotalNetPayable,
                ClaimableTaxAmount = 0m,
                PendingAmount = 0m,
                Description = $"Payroll period {x.Year}-{x.Month:00}",
                Notes = null
            })
            .ToListAsync(cancellationToken);

        return receivedInvoices
            .Concat(rentEntries)
            .Concat(manualEntries)
            .Concat(payrollRows)
            .ToList();
    }

    private async Task<List<ExpenseLedgerRowDto>> GetFilteredLedgerRowsAsync(ExpenseLedgerQuery query, CancellationToken cancellationToken)
    {
        var rows = await BuildLedgerRowsAsync(query.DateFrom, query.DateTo, cancellationToken);
        rows = ApplyLedgerFilters(rows, query);

        return query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderBy(x => x.TransactionDate).ThenBy(x => x.DocumentNumber).ToList()
            : rows.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.DocumentNumber).ToList();
    }

    private static ExpenseSummaryDto BuildSummary(IReadOnlyList<ExpenseLedgerRowDto> rows)
    {
        return new ExpenseSummaryDto
        {
            TotalNetAmount = rows.Sum(x => x.NetAmount),
            TotalTaxAmount = rows.Sum(x => x.TaxAmount),
            TotalGrossAmount = rows.Sum(x => x.GrossAmount),
            TotalPendingAmount = rows.Sum(x => x.PendingAmount),
            ByCategory = rows
                .GroupBy(x => x.ExpenseCategoryName)
                .OrderByDescending(x => x.Sum(row => row.GrossAmount))
                .Select(group => new ExpenseSummaryBucketDto
                {
                    Label = group.Key,
                    NetAmount = group.Sum(x => x.NetAmount),
                    TaxAmount = group.Sum(x => x.TaxAmount),
                    GrossAmount = group.Sum(x => x.GrossAmount)
                })
                .ToList(),
            ByMonth = rows
                .GroupBy(x => $"{x.TransactionDate:yyyy-MM}")
                .OrderBy(x => x.Key)
                .Select(group => new ExpenseSummaryBucketDto
                {
                    Label = group.Key,
                    NetAmount = group.Sum(x => x.NetAmount),
                    TaxAmount = group.Sum(x => x.TaxAmount),
                    GrossAmount = group.Sum(x => x.GrossAmount)
                })
                .ToList()
        };
    }

    private static List<ExpenseLedgerRowDto> ApplyLedgerFilters(IEnumerable<ExpenseLedgerRowDto> rows, ExpenseLedgerQuery query)
    {
        var filtered = rows;

        if (query.ExpenseCategoryId.HasValue)
        {
            filtered = filtered.Where(x => x.ExpenseCategoryId == query.ExpenseCategoryId.Value);
        }

        if (query.SupplierId.HasValue)
        {
            filtered = filtered.Where(x => x.SupplierId == query.SupplierId.Value);
        }

        if (query.SourceType.HasValue)
        {
            filtered = filtered.Where(x => string.Equals(x.SourceType, query.SourceType.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        if (query.PendingOnly)
        {
            filtered = filtered.Where(x => x.PendingAmount > 0m);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                x.DocumentNumber.ToLowerInvariant().Contains(search)
                || x.PayeeName.ToLowerInvariant().Contains(search)
                || x.ExpenseCategoryName.ToLowerInvariant().Contains(search)
                || (!string.IsNullOrWhiteSpace(x.SupplierName) && x.SupplierName.ToLowerInvariant().Contains(search))
                || (!string.IsNullOrWhiteSpace(x.Description) && x.Description.ToLowerInvariant().Contains(search)));
        }

        return filtered.ToList();
    }

    private static string BuildFilterLabel(ExpenseLedgerQuery query)
    {
        var parts = new List<string>();
        if (query.DateFrom.HasValue || query.DateTo.HasValue)
        {
            parts.Add($"Range: {(query.DateFrom?.ToString("yyyy-MM-dd") ?? "Start")} to {(query.DateTo?.ToString("yyyy-MM-dd") ?? "Today")}");
        }

        if (query.SourceType.HasValue)
        {
            parts.Add($"Source: {query.SourceType.Value}");
        }

        if (query.PendingOnly)
        {
            parts.Add("Pending only");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"Search: {query.Search.Trim()}");
        }

        return parts.Count == 0 ? "All live expense rows" : string.Join(" | ", parts);
    }

    private static string FormatDualCurrency(IEnumerable<ExpenseLedgerRowDto> rows, Func<ExpenseLedgerRowDto, decimal> selector)
    {
        var mvrTotal = rows
            .Where(x => !string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .Sum(selector);
        var usdTotal = rows
            .Where(x => string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .Sum(selector);

        return $"MVR {mvrTotal:N2} | USD {usdTotal:N2}";
    }

    private static void WriteMetricTile(IXLWorksheet sheet, int row, int startColumn, int endColumn, string label, string value)
    {
        var range = sheet.Range(row, startColumn, row + 1, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF4FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFDCF6");
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var richText = sheet.Cell(row, startColumn).GetRichText();
        richText.ClearText();
        richText.AddText(label.ToUpperInvariant()).SetBold().SetFontSize(9).SetFontColor(XLColor.FromHtml("#5D71A5"));
        richText.AddText(Environment.NewLine);
        richText.AddText(value).SetBold().SetFontSize(12).SetFontColor(XLColor.FromHtml("#243B6B"));
    }

    private static void StyleExcelTableHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        range.Style.Font.FontColor = XLColor.FromHtml("#546E9E");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1DDF5");
    }

    private static void StyleExcelBodyRows(IXLRange range)
    {
        foreach (var row in range.Rows())
        {
            row.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            row.Style.Border.BottomBorderColor = XLColor.FromHtml("#E4EBFB");
            if ((row.RowNumber() - range.FirstRow().RowNumber()) % 2 == 1)
            {
                row.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFCFF");
            }
        }
    }

    private static void WriteSectionHeader(IXLRange range)
    {
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#243B6B");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#CFDCF6");
    }

    private async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var hasAny = await _dbContext.ExpenseCategories.AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (hasAny)
        {
            return;
        }

        _dbContext.ExpenseCategories.AddRange(ExpenseCategoryService.BuildDefaultCategories(tenantId));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var currency = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToUpperInvariant();
        return currency == "USD" ? "USD" : "MVR";
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
