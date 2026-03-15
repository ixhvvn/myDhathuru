using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class MiraService : IMiraService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";

    private readonly ApplicationDbContext _dbContext;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public MiraService(
        ApplicationDbContext dbContext,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<MiraReportPreviewDto> GetPreviewAsync(MiraReportQuery query, CancellationToken cancellationToken = default)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var range = ResolveRange(query);

        return query.ReportType switch
        {
            MiraReportType.InputTaxStatement => await BuildInputTaxPreviewAsync(settings, range, cancellationToken),
            MiraReportType.OutputTaxStatement => await BuildOutputTaxPreviewAsync(settings, range, cancellationToken),
            MiraReportType.BptIncomeStatement => await BuildBptIncomePreviewAsync(settings, range, cancellationToken),
            MiraReportType.BptNotes => await BuildBptNotesPreviewAsync(settings, range, cancellationToken),
            _ => throw new AppException("Unsupported MIRA report type.")
        };
    }

    public async Task<ReportExportResultDto> ExportExcelAsync(MiraReportExportRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await GetPreviewAsync(request, cancellationToken);
        using var workbook = new XLWorkbook();

        switch (preview.ReportType)
        {
            case MiraReportType.InputTaxStatement:
                BuildInputTaxWorkbook(workbook, preview);
                break;
            case MiraReportType.OutputTaxStatement:
                BuildOutputTaxWorkbook(workbook, preview);
                break;
            case MiraReportType.BptIncomeStatement:
                BuildBptIncomeWorkbook(workbook, preview);
                break;
            case MiraReportType.BptNotes:
                BuildBptNotesWorkbook(workbook, preview);
                break;
            default:
                throw new AppException("Unsupported MIRA report type.");
        }

        return new ReportExportResultDto
        {
            FileName = BuildFileName(preview, "xlsx"),
            ContentType = ExcelContentType,
            Content = SaveWorkbook(workbook)
        };
    }

    public async Task<ReportExportResultDto> ExportPdfAsync(MiraReportExportRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await GetPreviewAsync(request, cancellationToken);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = BuildCompanyInfo(settings.TinNumber, settings.CompanyPhone, settings.CompanyEmail);

        var bytes = preview.ReportType switch
        {
            MiraReportType.InputTaxStatement => _pdfExportService.BuildMiraInputTaxStatementPdf(preview, settings.CompanyName, companyInfo, settings.LogoUrl),
            MiraReportType.OutputTaxStatement => _pdfExportService.BuildMiraOutputTaxStatementPdf(preview, settings.CompanyName, companyInfo, settings.LogoUrl),
            MiraReportType.BptIncomeStatement => _pdfExportService.BuildBptIncomeStatementPdf(preview, settings.CompanyName, companyInfo, settings.LogoUrl),
            MiraReportType.BptNotes => _pdfExportService.BuildBptNotesPdf(preview, settings.CompanyName, companyInfo, settings.LogoUrl),
            _ => throw new AppException("Unsupported MIRA report type.")
        };

        return new ReportExportResultDto
        {
            FileName = BuildFileName(preview, "pdf"),
            ContentType = PdfContentType,
            Content = bytes
        };
    }

    private async Task<MiraReportPreviewDto> BuildInputTaxPreviewAsync(TenantSettings settings, MiraRangeContext range, CancellationToken cancellationToken)
    {
        var invoices = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved
                && x.IsTaxClaimable)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var usdTotal = invoices.Where(x => !IsMvr(x.Currency)).Sum(x => x.TotalAmount);
        var rows = invoices
            .Where(x => IsMvr(x.Currency) && x.GstAmount > 0)
            .Select(x =>
            {
                var lineBuckets = x.Items.Count > 0
                    ? x.Items
                    : new List<ReceivedInvoiceItem>
                    {
                        new()
                        {
                            Description = "GST",
                            GstRate = x.GstRate,
                            GstAmount = x.GstAmount,
                            LineTotal = x.Subtotal
                        }
                    };

                return new MiraInputTaxRowDto
                {
                    SupplierTin = Safe(x.SupplierTin),
                    SupplierName = Safe(x.SupplierName),
                    SupplierInvoiceNumber = x.InvoiceNumber,
                    InvoiceDate = x.InvoiceDate,
                    Currency = NormalizeCurrency(x.Currency),
                    InvoiceTotalExcludingGst = Round2(x.Subtotal > 0 ? x.Subtotal : x.TotalAmount - x.GstAmount),
                    GstChargedAt6 = Round2(lineBuckets.Where(i => NormalizeRatePercent(i.GstRate) == 6m).Sum(i => i.GstAmount)),
                    GstChargedAt8 = Round2(lineBuckets.Where(i => NormalizeRatePercent(i.GstRate) == 8m).Sum(i => i.GstAmount)),
                    GstChargedAt12 = Round2(lineBuckets.Where(i => NormalizeRatePercent(i.GstRate) == 12m).Sum(i => i.GstAmount)),
                    GstChargedAt16 = Round2(lineBuckets.Where(i => NormalizeRatePercent(i.GstRate) == 16m).Sum(i => i.GstAmount)),
                    GstChargedAt17 = Round2(lineBuckets.Where(i => NormalizeRatePercent(i.GstRate) == 17m).Sum(i => i.GstAmount)),
                    TotalGst = Round2(x.GstAmount),
                    TaxableActivityNumber = Safe(string.IsNullOrWhiteSpace(x.MiraTaxableActivityNumber) ? settings.TaxableActivityNumber : x.MiraTaxableActivityNumber)
                };
            })
            .ToList();

        var statement = new MiraInputTaxStatementDto
        {
            TotalInvoices = rows.Count,
            TotalInvoiceBase = Round2(rows.Sum(x => x.InvoiceTotalExcludingGst)),
            TotalGst6 = Round2(rows.Sum(x => x.GstChargedAt6)),
            TotalGst8 = Round2(rows.Sum(x => x.GstChargedAt8)),
            TotalGst12 = Round2(rows.Sum(x => x.GstChargedAt12)),
            TotalGst16 = Round2(rows.Sum(x => x.GstChargedAt16)),
            TotalGst17 = Round2(rows.Sum(x => x.GstChargedAt17)),
            TotalClaimableGst = Round2(rows.Sum(x => x.TotalGst)),
            Rows = rows
        };

        return new MiraReportPreviewDto
        {
            ReportType = MiraReportType.InputTaxStatement,
            Meta = BuildMeta(settings, range, "MIRA Input Tax Statement", usdExpenseTotal: usdTotal),
            Assumptions = BuildAssumptions(MiraReportType.InputTaxStatement, usdExpenseTotal: usdTotal),
            InputTaxStatement = statement
        };
    }

    private async Task<MiraReportPreviewDto> BuildOutputTaxPreviewAsync(TenantSettings settings, MiraRangeContext range, CancellationToken cancellationToken)
    {
        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.DateIssued >= range.StartDate && x.DateIssued <= range.EndDate)
            .OrderBy(x => x.DateIssued)
            .ThenBy(x => x.InvoiceNo)
            .ToListAsync(cancellationToken);

        var usdTotal = invoices.Where(x => !IsMvr(x.Currency)).Sum(x => x.Subtotal);
        var rows = invoices
            .Where(x => IsMvr(x.Currency))
            .Select(x => new MiraOutputTaxRowDto
            {
                CustomerTin = Safe(x.Customer?.TinNumber),
                CustomerName = Safe(x.Customer?.Name),
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = x.DateIssued,
                Currency = NormalizeCurrency(x.Currency),
                TaxableSupplies = x.TaxAmount > 0 || x.TaxRate > 0 ? Round2(x.Subtotal) : 0m,
                ZeroRatedSupplies = 0m,
                ExemptSupplies = 0m,
                OutOfScopeSupplies = x.TaxAmount > 0 || x.TaxRate > 0 ? 0m : Round2(x.Subtotal),
                GstRate = NormalizeRatePercent(x.TaxRate),
                GstAmount = Round2(x.TaxAmount),
                TaxableActivityNumber = Safe(settings.TaxableActivityNumber)
            })
            .ToList();

        var statement = new MiraOutputTaxStatementDto
        {
            TotalInvoices = rows.Count,
            TotalTaxableSupplies = Round2(rows.Sum(x => x.TaxableSupplies)),
            TotalZeroRatedSupplies = Round2(rows.Sum(x => x.ZeroRatedSupplies)),
            TotalExemptSupplies = Round2(rows.Sum(x => x.ExemptSupplies)),
            TotalOutOfScopeSupplies = Round2(rows.Sum(x => x.OutOfScopeSupplies)),
            TotalTaxAmount = Round2(rows.Sum(x => x.GstAmount)),
            Rows = rows
        };

        return new MiraReportPreviewDto
        {
            ReportType = MiraReportType.OutputTaxStatement,
            Meta = BuildMeta(settings, range, "MIRA Output Tax Statement", usdRevenueTotal: usdTotal),
            Assumptions = BuildAssumptions(MiraReportType.OutputTaxStatement, usdRevenueTotal: usdTotal),
            OutputTaxStatement = statement
        };
    }

    private async Task<MiraReportPreviewDto> BuildBptIncomePreviewAsync(TenantSettings settings, MiraRangeContext range, CancellationToken cancellationToken)
    {
        var grossSales = await GetGrossSalesAsync(range, cancellationToken);
        var salaryRows = await GetSalaryRowsAsync(range, cancellationToken);
        var expenseRows = await GetExpenseRowsAsync(range, cancellationToken);

        var usdRevenueTotal = grossSales.Where(x => !IsMvr(x.Currency)).Sum(x => x.Amount);
        var usdExpenseTotal = expenseRows.Where(x => !IsMvr(x.Currency)).Sum(x => x.Amount);

        var mvrExpenseRows = expenseRows.Where(x => IsMvr(x.Currency)).ToList();
        var salaryTotal = Round2(salaryRows.Sum(x => x.TotalForPeriodRange));
        var costOfGoodsSold = Round2(mvrExpenseRows
            .Where(x => x.CategoryCode == BptCategoryCode.DieselCharges)
            .Sum(x => x.Amount));

        var operatingExpenseLines = BuildOperatingExpenseLines(mvrExpenseRows, salaryTotal);
        var totalOperatingExpenses = Round2(operatingExpenseLines.Sum(x => x.Amount));
        var netSales = Round2(grossSales.Where(x => IsMvr(x.Currency)).Sum(x => x.Amount));
        var grossProfit = Round2(netSales - costOfGoodsSold);
        var netOperatingIncome = Round2(grossProfit - totalOperatingExpenses);

        var statement = new BptIncomeStatementDto
        {
            GrossSales = netSales,
            SalesReturnsAndAllowances = 0m,
            NetSales = netSales,
            CostOfGoodsSold = costOfGoodsSold,
            GrossProfit = grossProfit,
            OperatingExpenses = operatingExpenseLines,
            TotalOperatingExpenses = totalOperatingExpenses,
            NetOperatingIncome = netOperatingIncome,
            OtherIncome = new List<BptIncomeLineDto>(),
            TotalOtherIncome = 0m,
            NetIncome = netOperatingIncome
        };

        return new MiraReportPreviewDto
        {
            ReportType = MiraReportType.BptIncomeStatement,
            Meta = BuildMeta(settings, range, "BPT Income Statement", usdRevenueTotal, usdExpenseTotal),
            Assumptions = BuildAssumptions(MiraReportType.BptIncomeStatement, usdRevenueTotal, usdExpenseTotal),
            BptIncomeStatement = statement
        };
    }

    private async Task<MiraReportPreviewDto> BuildBptNotesPreviewAsync(TenantSettings settings, MiraRangeContext range, CancellationToken cancellationToken)
    {
        var salaryRows = await GetSalaryRowsAsync(range, cancellationToken);
        var expenseRows = await GetExpenseRowsAsync(range, cancellationToken);

        var usdExpenseTotal = expenseRows.Where(x => !IsMvr(x.Currency)).Sum(x => x.Amount);
        var sections = expenseRows
            .Where(x => IsMvr(x.Currency) && x.CategoryCode != BptCategoryCode.Salary)
            .GroupBy(x => x.CategoryCode)
            .OrderBy(g => (int)g.Key)
            .Select(g => new BptExpenseNoteSectionDto
            {
                CategoryCode = g.Key,
                Title = GetBptCategoryTitle(g.Key),
                TotalAmount = Round2(g.Sum(x => x.Amount)),
                Rows = g.OrderBy(x => x.Date)
                    .ThenBy(x => x.DocumentNumber)
                    .Select(x => new BptExpenseNoteRowDto
                    {
                        Date = x.Date,
                        DocumentNumber = x.DocumentNumber,
                        PayeeName = x.PayeeName,
                        Detail = x.Detail,
                        Amount = Round2(x.Amount)
                    })
                    .ToList()
            })
            .ToList();

        var notes = new BptNotesDto
        {
            SalaryRows = salaryRows,
            TotalSalary = Round2(salaryRows.Sum(x => x.TotalForPeriodRange)),
            Sections = sections
        };

        return new MiraReportPreviewDto
        {
            ReportType = MiraReportType.BptNotes,
            Meta = BuildMeta(settings, range, "BPT Notes", usdExpenseTotal: usdExpenseTotal),
            Assumptions = BuildAssumptions(MiraReportType.BptNotes, usdExpenseTotal: usdExpenseTotal),
            BptNotes = notes
        };
    }

    private async Task<List<(string Currency, decimal Amount)>> GetGrossSalesAsync(MiraRangeContext range, CancellationToken cancellationToken)
    {
        return await _dbContext.Invoices
            .AsNoTracking()
            .Where(x => x.DateIssued >= range.StartDate && x.DateIssued <= range.EndDate)
            .Select(x => new ValueTuple<string, decimal>(x.Currency, x.Subtotal))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<BptSalaryNoteRowDto>> GetSalaryRowsAsync(MiraRangeContext range, CancellationToken cancellationToken)
    {
        var payrollEntries = await _dbContext.PayrollEntries
            .AsNoTracking()
            .Include(x => x.Staff)
            .Include(x => x.PayrollPeriod)
            .Where(x => x.PayrollPeriod.EndDate >= range.StartDate && x.PayrollPeriod.EndDate <= range.EndDate)
            .OrderBy(x => x.Staff.StaffName)
            .ToListAsync(cancellationToken);

        return payrollEntries
            .GroupBy(x => new { x.Staff.StaffId, x.Staff.StaffName })
            .Select(g => new BptSalaryNoteRowDto
            {
                StaffCode = g.Key.StaffId,
                StaffName = g.Key.StaffName,
                AverageBasicPerPeriod = Round2(g.Average(x => x.Basic)),
                AverageAllowancePerPeriod = Round2(g.Average(x => x.ServiceAllowance + x.OtherAllowance + x.PhoneAllowance + x.FoodAllowance + x.OvertimePay)),
                PeriodCount = g.Select(x => x.PayrollPeriodId).Distinct().Count(),
                TotalForPeriodRange = Round2(g.Sum(x => x.TotalPay))
            })
            .OrderBy(x => x.StaffName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<BptExpenseSourceRow>> GetExpenseRowsAsync(MiraRangeContext range, CancellationToken cancellationToken)
    {
        var receivedInvoices = await _dbContext.ReceivedInvoices
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.InvoiceDate >= range.StartDate
                && x.InvoiceDate <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved
                && x.RevenueCapitalClassification == RevenueCapitalType.Revenue)
            .Select(x => new BptExpenseSourceRow
            {
                Date = x.InvoiceDate,
                DocumentNumber = x.InvoiceNumber,
                PayeeName = x.SupplierName,
                Detail = string.IsNullOrWhiteSpace(x.Description) ? x.SupplierName : x.Description!,
                Currency = x.Currency,
                CategoryCode = x.ExpenseCategory.BptCategoryCode,
                Amount = x.IsTaxClaimable ? x.TotalAmount - x.GstAmount : x.TotalAmount
            })
            .ToListAsync(cancellationToken);

        var rentEntries = await _dbContext.RentEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.Date >= range.StartDate
                && x.Date <= range.EndDate
                && x.ApprovalStatus == ApprovalStatus.Approved)
            .Select(x => new BptExpenseSourceRow
            {
                Date = x.Date,
                DocumentNumber = x.RentNumber,
                PayeeName = x.PayTo,
                Detail = string.IsNullOrWhiteSpace(x.PropertyName) ? x.PayTo : x.PropertyName,
                Currency = x.Currency,
                CategoryCode = x.ExpenseCategory.BptCategoryCode,
                Amount = x.Amount
            })
            .ToListAsync(cancellationToken);

        var manualEntries = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(x => x.ExpenseCategory)
            .Where(x => x.SourceType == ExpenseSourceType.Manual
                && x.TransactionDate >= range.StartDate
                && x.TransactionDate <= range.EndDate)
            .Select(x => new BptExpenseSourceRow
            {
                Date = x.TransactionDate,
                DocumentNumber = x.DocumentNumber,
                PayeeName = x.PayeeName,
                Detail = string.IsNullOrWhiteSpace(x.Description) ? x.PayeeName : x.Description!,
                Currency = x.Currency,
                CategoryCode = x.ExpenseCategory.BptCategoryCode,
                Amount = x.GrossAmount - x.ClaimableTaxAmount
            })
            .ToListAsync(cancellationToken);

        return receivedInvoices
            .Concat(rentEntries)
            .Concat(manualEntries)
            .ToList();
    }

    private static List<BptIncomeLineDto> BuildOperatingExpenseLines(IEnumerable<BptExpenseSourceRow> expenseRows, decimal salaryTotal)
    {
        var lines = expenseRows
            .Where(x => x.CategoryCode != BptCategoryCode.DieselCharges)
            .GroupBy(x => x.CategoryCode)
            .Select(g => new BptIncomeLineDto
            {
                Label = GetBptCategoryTitle(g.Key),
                Amount = Round2(g.Sum(x => x.Amount))
            })
            .Where(x => x.Amount != 0m)
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (salaryTotal > 0)
        {
            lines.RemoveAll(x => string.Equals(x.Label, GetBptCategoryTitle(BptCategoryCode.Salary), StringComparison.OrdinalIgnoreCase));
            lines.Insert(0, new BptIncomeLineDto
            {
                Label = GetBptCategoryTitle(BptCategoryCode.Salary),
                Amount = salaryTotal
            });
        }

        return lines;
    }

    private static MiraReportMetaDto BuildMeta(TenantSettings settings, MiraRangeContext range, string title, decimal usdRevenueTotal = 0m, decimal usdExpenseTotal = 0m)
    {
        return new MiraReportMetaDto
        {
            Title = title,
            PeriodLabel = range.Label,
            RangeStart = range.StartDate,
            RangeEnd = range.EndDate,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TaxableActivityNumber = Safe(settings.TaxableActivityNumber),
            CompanyName = settings.CompanyName,
            CompanyTinNumber = settings.TinNumber,
            HasUsdTransactions = usdRevenueTotal > 0 || usdExpenseTotal > 0,
            UnconvertedUsdRevenue = Round2(usdRevenueTotal),
            UnconvertedUsdExpenses = Round2(usdExpenseTotal)
        };
    }

    private static List<string> BuildAssumptions(MiraReportType reportType, decimal usdRevenueTotal = 0m, decimal usdExpenseTotal = 0m)
    {
        var assumptions = new List<string>();

        if (usdRevenueTotal > 0 || usdExpenseTotal > 0)
        {
            assumptions.Add("USD transactions are excluded from statutory totals because the system does not yet store filing-period exchange rates.");
        }

        if (reportType == MiraReportType.OutputTaxStatement)
        {
            assumptions.Add("Invoices without GST are classified under out-of-scope supplies because zero-rated and exempt classifications are not stored separately.");
        }

        if (reportType is MiraReportType.BptIncomeStatement or MiraReportType.BptNotes)
        {
            assumptions.Add("Sales returns, allowances, and other income are reported as zero unless separate ledger entries are introduced.");
            assumptions.Add("BPT cost of goods sold is currently mapped from expenses categorized as Diesel Charges.");
            assumptions.Add("Salary notes are grouped from payroll data only; nationality splits are unavailable in the current schema.");
        }

        return assumptions;
    }

    private static void BuildInputTaxWorkbook(XLWorkbook workbook, MiraReportPreviewDto preview)
    {
        var model = preview.InputTaxStatement ?? throw new AppException("Input tax statement preview is missing.");
        var sheet = workbook.Worksheets.Add("InputTaxStatement");
        var row = 1;

        row = BuildWorkbookHeader(sheet, preview.Meta, row, "Input Tax Statement", preview.Assumptions);
        row += 1;
        row = WriteMetricStrip(sheet, row, new[]
        {
            ("Invoices", model.TotalInvoices.ToString(CultureInfo.InvariantCulture)),
            ("Taxable Base", model.TotalInvoiceBase.ToString("N2", CultureInfo.InvariantCulture)),
            ("Claimable GST", model.TotalClaimableGst.ToString("N2", CultureInfo.InvariantCulture)),
            ("Activity No.", preview.Meta.TaxableActivityNumber)
        });
        row += 1;

        var headers = new[]
        {
            "#", "Supplier TIN", "Supplier Name", "Supplier Invoice Number", "Invoice Date", "Invoice Total (Excl GST)",
            "GST @6%", "GST @8%", "GST @12%", "GST @16%", "GST @17%", "Total GST", "Your Taxable Activity Number"
        };
        WriteTableHeaders(sheet, row, headers);
        var dataStart = row + 1;
        row = dataStart;

        for (var index = 0; index < model.Rows.Count; index++, row++)
        {
            var item = model.Rows[index];
            sheet.Cell(row, 1).Value = index + 1;
            sheet.Cell(row, 2).Value = item.SupplierTin;
            sheet.Cell(row, 3).Value = item.SupplierName;
            sheet.Cell(row, 4).Value = item.SupplierInvoiceNumber;
            sheet.Cell(row, 5).Value = item.InvoiceDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 6).Value = item.InvoiceTotalExcludingGst;
            sheet.Cell(row, 7).Value = item.GstChargedAt6;
            sheet.Cell(row, 8).Value = item.GstChargedAt8;
            sheet.Cell(row, 9).Value = item.GstChargedAt12;
            sheet.Cell(row, 10).Value = item.GstChargedAt16;
            sheet.Cell(row, 11).Value = item.GstChargedAt17;
            sheet.Cell(row, 12).Value = item.TotalGst;
            sheet.Cell(row, 13).Value = item.TaxableActivityNumber;
        }

        WriteTotalsRow(sheet, row, new object[]
        {
            "", "", "", "", "Total",
            model.TotalInvoiceBase, model.TotalGst6, model.TotalGst8, model.TotalGst12, model.TotalGst16, model.TotalGst17, model.TotalClaimableGst, ""
        });
        StyleMiraStatementSheet(sheet, dataStart, row, headers.Length, dateColumn: 5, numericFromColumn: 6);
    }

    private static void BuildOutputTaxWorkbook(XLWorkbook workbook, MiraReportPreviewDto preview)
    {
        var model = preview.OutputTaxStatement ?? throw new AppException("Output tax statement preview is missing.");

        var detailSheet = workbook.Worksheets.Add("TaxInvoices");
        var row = 1;
        row = BuildWorkbookHeader(detailSheet, preview.Meta, row, "Output Tax Statement", preview.Assumptions);
        row += 1;
        row = WriteMetricStrip(detailSheet, row, new[]
        {
            ("Invoices", model.TotalInvoices.ToString(CultureInfo.InvariantCulture)),
            ("Taxable", model.TotalTaxableSupplies.ToString("N2", CultureInfo.InvariantCulture)),
            ("Out of Scope", model.TotalOutOfScopeSupplies.ToString("N2", CultureInfo.InvariantCulture)),
            ("GST", model.TotalTaxAmount.ToString("N2", CultureInfo.InvariantCulture))
        });
        row += 1;

        var headers = new[]
        {
            "#", "Customer TIN", "Customer Name", "Invoice No.", "Invoice Date", "Taxable Supplies", "Zero Rated", "Exempt", "Out of Scope", "GST Rate", "GST Amount", "Your Taxable Activity No."
        };
        WriteTableHeaders(detailSheet, row, headers);
        var dataStart = row + 1;
        row = dataStart;

        for (var index = 0; index < model.Rows.Count; index++, row++)
        {
            var item = model.Rows[index];
            detailSheet.Cell(row, 1).Value = index + 1;
            detailSheet.Cell(row, 2).Value = item.CustomerTin;
            detailSheet.Cell(row, 3).Value = item.CustomerName;
            detailSheet.Cell(row, 4).Value = item.InvoiceNo;
            detailSheet.Cell(row, 5).Value = item.InvoiceDate.ToDateTime(TimeOnly.MinValue);
            detailSheet.Cell(row, 6).Value = item.TaxableSupplies;
            detailSheet.Cell(row, 7).Value = item.ZeroRatedSupplies;
            detailSheet.Cell(row, 8).Value = item.ExemptSupplies;
            detailSheet.Cell(row, 9).Value = item.OutOfScopeSupplies;
            detailSheet.Cell(row, 10).Value = item.GstRate;
            detailSheet.Cell(row, 11).Value = item.GstAmount;
            detailSheet.Cell(row, 12).Value = item.TaxableActivityNumber;
        }

        WriteTotalsRow(detailSheet, row, new object[]
        {
            "", "", "", "", "Total",
            model.TotalTaxableSupplies, model.TotalZeroRatedSupplies, model.TotalExemptSupplies, model.TotalOutOfScopeSupplies, "", model.TotalTaxAmount, ""
        });
        StyleMiraStatementSheet(detailSheet, dataStart, row, headers.Length, dateColumn: 5, numericFromColumn: 6);

        var summarySheet = workbook.Worksheets.Add("OtherTransactions");
        row = 1;
        row = BuildWorkbookHeader(summarySheet, preview.Meta, row, "Other Transactions Summary", preview.Assumptions);
        row += 2;
        WriteTableHeaders(summarySheet, row, new[]
        {
            "Your Taxable Activity No.", "Taxable Supplies", "Zero Rated", "Exempt", "Out of Scope"
        });
        row += 1;
        summarySheet.Cell(row, 1).Value = preview.Meta.TaxableActivityNumber;
        summarySheet.Cell(row, 2).Value = model.TotalTaxableSupplies;
        summarySheet.Cell(row, 3).Value = model.TotalZeroRatedSupplies;
        summarySheet.Cell(row, 4).Value = model.TotalExemptSupplies;
        summarySheet.Cell(row, 5).Value = model.TotalOutOfScopeSupplies;
        StyleMiraStatementSheet(summarySheet, row, row, 5, dateColumn: null, numericFromColumn: 2);
    }

    private static void BuildBptIncomeWorkbook(XLWorkbook workbook, MiraReportPreviewDto preview)
    {
        var model = preview.BptIncomeStatement ?? throw new AppException("BPT income statement preview is missing.");
        var sheet = workbook.Worksheets.Add("BPTIncomeStatement");
        var row = 1;

        row = BuildWorkbookHeader(sheet, preview.Meta, row, "BPT Income Statement", preview.Assumptions);
        row += 1;
        row = WriteMetricStrip(sheet, row, new[]
        {
            ("Net Sales", model.NetSales.ToString("N2", CultureInfo.InvariantCulture)),
            ("Gross Profit", model.GrossProfit.ToString("N2", CultureInfo.InvariantCulture)),
            ("Operating Expenses", model.TotalOperatingExpenses.ToString("N2", CultureInfo.InvariantCulture)),
            ("Net Income", model.NetIncome.ToString("N2", CultureInfo.InvariantCulture))
        });
        row += 2;

        sheet.Cell(row, 1).Value = "Revenue";
        StyleSectionRow(sheet, row, 2);
        row++;
        row = WriteLabelValue(sheet, row, "Gross sales", model.GrossSales);
        row = WriteLabelValue(sheet, row, "Sales returns and allowances", model.SalesReturnsAndAllowances);
        row = WriteTotalLabelValue(sheet, row, "Net sales", model.NetSales);
        row++;
        sheet.Cell(row, 1).Value = "Cost of Goods Sold";
        StyleSectionRow(sheet, row, 2);
        row++;
        row = WriteLabelValue(sheet, row, "Diesel charges / direct cost", model.CostOfGoodsSold);
        row = WriteGrandTotalLabelValue(sheet, row, "Gross profit", model.GrossProfit);
        row++;
        sheet.Cell(row, 1).Value = "Operating Expenses";
        StyleSectionRow(sheet, row, 2);
        row++;
        foreach (var item in model.OperatingExpenses)
        {
            row = WriteLabelValue(sheet, row, item.Label, item.Amount);
        }

        row = WriteTotalLabelValue(sheet, row, "Total operating expenses", model.TotalOperatingExpenses);
        row = WriteGrandTotalLabelValue(sheet, row, "Net operating income", model.NetOperatingIncome);
        row++;
        sheet.Cell(row, 1).Value = "Other Income";
        StyleSectionRow(sheet, row, 2);
        row++;
        if (model.OtherIncome.Count == 0)
        {
            row = WriteLabelValue(sheet, row, "No separate other income recorded", 0m);
        }
        else
        {
            foreach (var item in model.OtherIncome)
            {
                row = WriteLabelValue(sheet, row, item.Label, item.Amount);
            }
        }

        row = WriteTotalLabelValue(sheet, row, "Total other income", model.TotalOtherIncome);
        row = WriteGrandTotalLabelValue(sheet, row, "Net income", model.NetIncome);

        sheet.Columns().AdjustToContents();
        sheet.Column(1).Width = 42;
        sheet.Column(2).Width = 18;
        sheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Column(2).Style.NumberFormat.Format = "#,##0.00";
    }

    private static void BuildBptNotesWorkbook(XLWorkbook workbook, MiraReportPreviewDto preview)
    {
        var model = preview.BptNotes ?? throw new AppException("BPT notes preview is missing.");

        var salarySheet = workbook.Worksheets.Add("SalaryDetails");
        var row = 1;
        row = BuildWorkbookHeader(salarySheet, preview.Meta, row, "BPT Notes - Salary Details", preview.Assumptions);
        row += 1;
        row = WriteMetricStrip(salarySheet, row, new[]
        {
            ("Staff Count", model.SalaryRows.Count.ToString(CultureInfo.InvariantCulture)),
            ("Total Salary", model.TotalSalary.ToString("N2", CultureInfo.InvariantCulture)),
            ("Period", preview.Meta.PeriodLabel),
            ("TIN", preview.Meta.CompanyTinNumber)
        });
        row += 1;
        WriteTableHeaders(salarySheet, row, new[] { "Staff ID", "Staff Name", "Avg Basic / Period", "Avg Allowance / Period", "Periods", "Total Salary" });
        var dataStart = row + 1;
        row = dataStart;

        foreach (var item in model.SalaryRows)
        {
            salarySheet.Cell(row, 1).Value = item.StaffCode;
            salarySheet.Cell(row, 2).Value = item.StaffName;
            salarySheet.Cell(row, 3).Value = item.AverageBasicPerPeriod;
            salarySheet.Cell(row, 4).Value = item.AverageAllowancePerPeriod;
            salarySheet.Cell(row, 5).Value = item.PeriodCount;
            salarySheet.Cell(row, 6).Value = item.TotalForPeriodRange;
            row++;
        }

        WriteTotalsRow(salarySheet, row, new object[] { "", "", "", "", "Total", model.TotalSalary });
        StyleMiraStatementSheet(salarySheet, dataStart, row, 6, dateColumn: null, numericFromColumn: 3);

        foreach (var section in model.Sections)
        {
            var safeName = new string(section.Title.Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
            var name = string.IsNullOrWhiteSpace(safeName) ? section.CategoryCode.ToString() : safeName;
            if (name.Length > 31)
            {
                name = name[..31];
            }

            var sheet = workbook.Worksheets.Add(name);
            row = 1;
            row = BuildWorkbookHeader(sheet, preview.Meta, row, $"BPT Notes - {section.Title}", preview.Assumptions);
            row += 1;
            row = WriteMetricStrip(sheet, row, new[]
            {
                ("Entries", section.Rows.Count.ToString(CultureInfo.InvariantCulture)),
                ("Total", section.TotalAmount.ToString("N2", CultureInfo.InvariantCulture)),
                ("Category", section.Title),
                ("Period", preview.Meta.PeriodLabel)
            });
            row += 1;
            WriteTableHeaders(sheet, row, new[] { "Date", "Document Number", "Payee", "Detail", "Amount" });
            dataStart = row + 1;
            row = dataStart;

            foreach (var item in section.Rows)
            {
                sheet.Cell(row, 1).Value = item.Date.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 2).Value = item.DocumentNumber;
                sheet.Cell(row, 3).Value = item.PayeeName;
                sheet.Cell(row, 4).Value = item.Detail;
                sheet.Cell(row, 5).Value = item.Amount;
                row++;
            }

            WriteTotalsRow(sheet, row, new object[] { "", "", "", "Total", section.TotalAmount });
            StyleMiraStatementSheet(sheet, dataStart, row, 5, dateColumn: 1, numericFromColumn: 5);
        }
    }

    private static int BuildWorkbookHeader(IXLWorksheet sheet, MiraReportMetaDto meta, int row, string subtitle, IReadOnlyCollection<string> assumptions)
    {
        sheet.Cell(row, 1).Value = meta.CompanyName;
        sheet.Range(row, 1, row, 6).Merge();
        sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 6).Style.Font.FontSize = 18;
        sheet.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.FromHtml("#243B6B");
        row++;

        sheet.Cell(row, 1).Value = subtitle;
        sheet.Range(row, 1, row, 6).Merge();
        sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 6).Style.Font.FontSize = 12;
        sheet.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.FromHtml("#5E72E4");
        row++;

        sheet.Cell(row, 1).Value = $"TIN: {meta.CompanyTinNumber}  |  Taxable Activity No.: {meta.TaxableActivityNumber}  |  Period: {meta.PeriodLabel}";
        sheet.Range(row, 1, row, 8).Merge();
        sheet.Range(row, 1, row, 8).Style.Font.FontColor = XLColor.FromHtml("#6E7F9B");
        row++;

        if (assumptions.Count > 0)
        {
            sheet.Cell(row, 1).Value = "Assumptions:";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#E28A1B");
            row++;
            foreach (var assumption in assumptions)
            {
                sheet.Cell(row, 1).Value = $"• {assumption}";
                sheet.Range(row, 1, row, 10).Merge();
                sheet.Range(row, 1, row, 10).Style.Font.FontColor = XLColor.FromHtml("#6E7F9B");
                row++;
            }
        }

        return row;
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

            sheet.Cell(row, startColumn).Value = metrics[index].Label.ToUpperInvariant();
            sheet.Cell(row, startColumn).Style.Font.Bold = true;
            sheet.Cell(row, startColumn).Style.Font.FontColor = XLColor.FromHtml("#5D71A5");
            sheet.Cell(row, startColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(row, startColumn).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Cell(row + 1, startColumn).Value = metrics[index].Value;
            sheet.Cell(row + 1, startColumn).Style.Font.Bold = true;
            sheet.Cell(row + 1, startColumn).Style.Font.FontSize = 14;
            sheet.Cell(row + 1, startColumn).Style.Font.FontColor = XLColor.FromHtml("#243B6B");
            sheet.Cell(row + 1, startColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        return row + 2;
    }

    private static void WriteTableHeaders(IXLWorksheet sheet, int row, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
            cell.Style.Font.FontColor = XLColor.FromHtml("#546E9E");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1DDF5");
        }
    }

    private static void WriteTotalsRow(IXLWorksheet sheet, int row, IReadOnlyList<object> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            switch (values[i])
            {
                case decimal decimalValue:
                    cell.Value = decimalValue;
                    break;
                case int intValue:
                    cell.Value = intValue;
                    break;
                case double doubleValue:
                    cell.Value = doubleValue;
                    break;
                case DateTime dateTimeValue:
                    cell.Value = dateTimeValue;
                    break;
                default:
                    cell.Value = values[i]?.ToString() ?? string.Empty;
                    break;
            }
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F7FF");
            cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.TopBorderColor = XLColor.FromHtml("#CFDCF6");
            cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#CFDCF6");
        }
    }

    private static void StyleMiraStatementSheet(IXLWorksheet sheet, int dataStartRow, int endRow, int totalColumns, int? dateColumn, int numericFromColumn)
    {
        var usedRange = sheet.Range(1, 1, endRow, totalColumns);
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Font.FontName = "Calibri";
        usedRange.Style.Font.FontSize = 10;

        if (dataStartRow <= endRow - 1)
        {
            var dataRange = sheet.Range(dataStartRow, 1, endRow - 1, totalColumns);
            dataRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            dataRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#E4EBFB");
            dataRange.Style.Fill.BackgroundColor = XLColor.White;
            foreach (var dataRow in dataRange.Rows())
            {
                if ((dataRow.RowNumber() - dataStartRow) % 2 == 1)
                {
                    dataRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFCFF");
                }
            }
        }

        if (dateColumn.HasValue)
        {
            sheet.Column(dateColumn.Value).Style.DateFormat.Format = "yyyy-MM-dd";
        }

        for (var column = numericFromColumn; column <= totalColumns; column++)
        {
            sheet.Column(column).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.SheetView.FreezeRows(dataStartRow - 1);
        sheet.Columns().AdjustToContents();
        sheet.Row(1).Height = 24;
    }

    private static void StyleSectionRow(IXLWorksheet sheet, int row, int totalColumns)
    {
        var range = sheet.Range(row, 1, row, totalColumns);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EAF1FF");
        range.Style.Font.FontColor = XLColor.FromHtml("#243B6B");
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#CFDCF6");
        range.Style.Border.BottomBorderColor = XLColor.FromHtml("#CFDCF6");
    }

    private static int WriteLabelValue(IXLWorksheet sheet, int row, string label, decimal value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = value;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        return row;
    }

    private static int WriteTotalLabelValue(IXLWorksheet sheet, int row, string label, decimal value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = value;
        StyleTotalLine(sheet, row);
        row++;
        return row;
    }

    private static int WriteGrandTotalLabelValue(IXLWorksheet sheet, int row, string label, decimal value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = value;
        StyleGrandTotalLine(sheet, row);
        row++;
        return row;
    }

    private static void StyleTotalLine(IXLWorksheet sheet, int row)
    {
        var range = sheet.Range(row, 1, row, 2);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F6F9FF");
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#CFDCF6");
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
    }

    private static void StyleGrandTotalLine(IXLWorksheet sheet, int row)
    {
        var range = sheet.Range(row, 1, row, 2);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF7F4");
        range.Style.Font.FontColor = XLColor.FromHtml("#1F4E43");
        range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.TopBorderColor = XLColor.FromHtml("#BADFD4");
        range.Style.Border.BottomBorderColor = XLColor.FromHtml("#BADFD4");
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string BuildFileName(MiraReportPreviewDto preview, string extension)
    {
        var slug = preview.ReportType switch
        {
            MiraReportType.InputTaxStatement => "mira-input-tax-statement",
            MiraReportType.OutputTaxStatement => "mira-output-tax-statement",
            MiraReportType.BptIncomeStatement => "bpt-income-statement",
            MiraReportType.BptNotes => "bpt-notes",
            _ => "mira-report"
        };

        var period = preview.Meta.RangeStart == preview.Meta.RangeEnd
            ? preview.Meta.RangeStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{preview.Meta.RangeStart:yyyy-MM-dd}_to_{preview.Meta.RangeEnd:yyyy-MM-dd}";

        return $"{slug}_{period}.{extension}";
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        if (!_currentTenantService.TenantId.HasValue)
        {
            throw new AppException("Tenant context is missing.");
        }

        return await _dbContext.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("Tenant settings are missing.");
    }

    private static MiraRangeContext ResolveRange(MiraReportQuery query)
    {
        return query.PeriodMode switch
        {
            MiraPeriodMode.Quarter => ResolveQuarterRange(query.Year, query.Quarter),
            MiraPeriodMode.Year => new MiraRangeContext(new DateOnly(query.Year, 1, 1), new DateOnly(query.Year, 12, 31), query.Year.ToString(CultureInfo.InvariantCulture)),
            MiraPeriodMode.CustomRange => ResolveCustomRange(query.CustomStartDate, query.CustomEndDate),
            _ => throw new AppException("Unsupported period mode.")
        };
    }

    private static MiraRangeContext ResolveQuarterRange(int year, int? quarter)
    {
        var resolvedQuarter = quarter is >= 1 and <= 4 ? quarter.Value : 1;
        var startMonth = ((resolvedQuarter - 1) * 3) + 1;
        var start = new DateOnly(year, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return new MiraRangeContext(start, end, $"Q{resolvedQuarter} {year}");
    }

    private static MiraRangeContext ResolveCustomRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            throw new AppException("Custom date range requires both start and end dates.");
        }

        if (endDate.Value < startDate.Value)
        {
            throw new AppException("End date cannot be earlier than start date.");
        }

        return new MiraRangeContext(startDate.Value, endDate.Value, $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
    }

    private static string BuildCompanyInfo(string tin, string? settingsPhone, string? settingsEmail)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(tin))
        {
            parts.Add($"TIN: {tin.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(settingsPhone))
        {
            parts.Add($"Phone: {settingsPhone.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(settingsEmail))
        {
            parts.Add($"Email: {settingsEmail.Trim()}");
        }

        return string.Join(" | ", parts);
    }

    private static decimal NormalizeRatePercent(decimal rate)
    {
        if (rate <= 0)
        {
            return 0m;
        }

        if (rate < 1m)
        {
            return Math.Round(rate * 100m, 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(rate, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "MVR" : currency.Trim().ToUpperInvariant();

    private static bool IsMvr(string? currency)
        => string.Equals(NormalizeCurrency(currency), "MVR", StringComparison.OrdinalIgnoreCase);

    private static string Safe(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string GetBptCategoryTitle(BptCategoryCode code)
    {
        return code switch
        {
            BptCategoryCode.Salary => "Salary details",
            BptCategoryCode.Rent => "Rent",
            BptCategoryCode.LicenseAndRegistrationFees => "License and registration fees",
            BptCategoryCode.OfficeExpense => "Office expense",
            BptCategoryCode.RepairAndMaintenance => "Repair and maintenance",
            BptCategoryCode.DieselCharges => "Diesel charges",
            BptCategoryCode.HiredFerryCharges => "Hired ferry charges",
            BptCategoryCode.UtilitiesTelephoneInternet => "Utilities / telephone / internet",
            BptCategoryCode.OfficeSupplies => "Office supplies",
            BptCategoryCode.Insurance => "Insurance",
            BptCategoryCode.LegalAndProfessionalFees => "Legal and professional fees",
            BptCategoryCode.Travel => "Travel",
            _ => "Other expenses"
        };
    }

    private sealed record MiraRangeContext(DateOnly StartDate, DateOnly EndDate, string Label);

    private sealed class BptExpenseSourceRow
    {
        public DateOnly Date { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
        public string PayeeName { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Currency { get; set; } = "MVR";
        public BptCategoryCode CategoryCode { get; set; }
        public decimal Amount { get; set; }
    }
}
