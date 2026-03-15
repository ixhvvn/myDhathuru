using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Infrastructure.Extensions;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class ReportService : IReportService
{
    private const string AllCustomersLabel = "All Customers";
    private const string UnassignedVesselLabel = "Unassigned Vessel";
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";
    private static readonly TimeSpan MaldivesOffset = TimeSpan.FromHours(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly IPdfExportService _pdfExportService;
    private readonly ICurrentTenantService _currentTenantService;

    public ReportService(
        ApplicationDbContext dbContext,
        IPdfExportService pdfExportService,
        ICurrentTenantService currentTenantService)
    {
        _dbContext = dbContext;
        _pdfExportService = pdfExportService;
        _currentTenantService = currentTenantService;
    }

    public async Task<SalesSummaryReportDto> GetSalesSummaryAsync(ReportFilterQuery query, CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(query, cancellationToken);
        var invoicesQuery = BuildInvoiceQuery(context);

        var groupedRows = await invoicesQuery
            .GroupBy(x => x.DateIssued)
            .Select(g => new
            {
                Date = g.Key,
                InvoiceCount = g.Count(),
                SalesMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.GrandTotal),
                SalesUsd = g.Where(x => x.Currency == "USD").Sum(x => x.GrandTotal),
                ReceivedMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.AmountPaid),
                ReceivedUsd = g.Where(x => x.Currency == "USD").Sum(x => x.AmountPaid),
                PendingMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.Balance),
                PendingUsd = g.Where(x => x.Currency == "USD").Sum(x => x.Balance),
                TaxMvr = g.Where(x => x.Currency == "MVR").Sum(x => x.TaxAmount),
                TaxUsd = g.Where(x => x.Currency == "USD").Sum(x => x.TaxAmount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        var totalCustomers = await invoicesQuery
            .Select(x => x.CustomerId)
            .Distinct()
            .CountAsync(cancellationToken);

        var rows = groupedRows.Select(x => new SalesSummaryDailyRowDto
        {
            Date = x.Date,
            InvoiceCount = x.InvoiceCount,
            SalesMvr = x.SalesMvr,
            SalesUsd = x.SalesUsd,
            ReceivedMvr = x.ReceivedMvr,
            ReceivedUsd = x.ReceivedUsd,
            PendingMvr = x.PendingMvr,
            PendingUsd = x.PendingUsd
        }).ToList();

        return new SalesSummaryReportDto
        {
            Meta = BuildMeta(context),
            TotalInvoices = rows.Sum(x => x.InvoiceCount),
            TotalSales = new CurrencyTotalsDto
            {
                Mvr = rows.Sum(x => x.SalesMvr),
                Usd = rows.Sum(x => x.SalesUsd)
            },
            TotalReceived = new CurrencyTotalsDto
            {
                Mvr = rows.Sum(x => x.ReceivedMvr),
                Usd = rows.Sum(x => x.ReceivedUsd)
            },
            TotalPending = new CurrencyTotalsDto
            {
                Mvr = rows.Sum(x => x.PendingMvr),
                Usd = rows.Sum(x => x.PendingUsd)
            },
            TotalCustomers = totalCustomers,
            TotalTax = new CurrencyTotalsDto
            {
                Mvr = groupedRows.Sum(x => x.TaxMvr),
                Usd = groupedRows.Sum(x => x.TaxUsd)
            },
            Rows = rows
        };
    }

    public async Task<SalesTransactionsReportDto> GetSalesTransactionsAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(query, cancellationToken);

        var invoices = await BuildInvoiceQuery(context)
            .AsSplitQuery()
            .Include(x => x.Customer)
            .Include(x => x.CourierVessel)
            .Include(x => x.DeliveryNote)
                .ThenInclude(x => x!.Vessel)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.DateIssued)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var rows = invoices.Select(invoice =>
        {
            var latestPayment = invoice.Payments
                .OrderByDescending(x => x.PaymentDate)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            var descriptions = invoice.Items
                .OrderBy(x => x.CreatedAt)
                .Select(x => x.Description.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var description = descriptions.Count == 0
                ? "-"
                : string.Join("; ", descriptions);

            if (description.Length > 220)
            {
                description = $"{description[..217]}...";
            }

            return new SalesTransactionRowDto
            {
                InvoiceNo = invoice.InvoiceNo,
                DateIssued = invoice.DateIssued,
                Customer = invoice.Customer.Name,
                Vessel = invoice.CourierVessel?.Name ?? invoice.DeliveryNote?.Vessel?.Name ?? UnassignedVesselLabel,
                Description = description,
                Currency = NormalizeCurrency(invoice.Currency),
                Amount = invoice.GrandTotal,
                PaymentStatus = invoice.PaymentStatus.ToString(),
                PaymentMethod = latestPayment?.Method.ToString() ?? "-",
                ReceivedOn = latestPayment?.PaymentDate,
                Balance = invoice.Balance
            };
        }).ToList();

        return new SalesTransactionsReportDto
        {
            Meta = BuildMeta(context),
            TotalTransactions = rows.Count,
            TotalSales = SumCurrencyRows(rows.Select(x => (x.Currency, x.Amount))),
            TotalReceived = SumCurrencyRows(invoices.Select(x => (NormalizeCurrency(x.Currency), x.AmountPaid))),
            TotalPending = SumCurrencyRows(rows.Select(x => (x.Currency, x.Balance))),
            Rows = rows
        };
    }

    public async Task<SalesByVesselReportDto> GetSalesByVesselAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(query, cancellationToken);

        var groupedRows = await BuildInvoiceQuery(context)
            .Select(x => new
            {
                Vessel = x.CourierVessel != null
                    ? x.CourierVessel.Name
                    : x.DeliveryNote != null && x.DeliveryNote.Vessel != null
                        ? x.DeliveryNote.Vessel.Name
                        : UnassignedVesselLabel,
                Currency = x.Currency,
                x.GrandTotal,
                x.AmountPaid,
                x.Balance
            })
            .GroupBy(x => new { x.Vessel, x.Currency })
            .Select(g => new
            {
                g.Key.Vessel,
                Currency = g.Key.Currency,
                TransactionCount = g.Count(),
                TotalSales = g.Sum(x => x.GrandTotal),
                TotalReceived = g.Sum(x => x.AmountPaid),
                PendingAmount = g.Sum(x => x.Balance)
            })
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => x.TotalSales)
            .ThenBy(x => x.Vessel)
            .ToListAsync(cancellationToken);

        var totalSalesMvr = groupedRows.Where(x => NormalizeCurrency(x.Currency) == "MVR").Sum(x => x.TotalSales);
        var totalSalesUsd = groupedRows.Where(x => NormalizeCurrency(x.Currency) == "USD").Sum(x => x.TotalSales);

        var rows = groupedRows.Select(x =>
        {
            var normalizedCurrency = NormalizeCurrency(x.Currency);
            var denominator = normalizedCurrency == "USD" ? totalSalesUsd : totalSalesMvr;

            return new SalesByVesselRowDto
            {
                Vessel = string.IsNullOrWhiteSpace(x.Vessel) ? UnassignedVesselLabel : x.Vessel,
                Currency = normalizedCurrency,
                TransactionCount = x.TransactionCount,
                TotalSales = x.TotalSales,
                TotalReceived = x.TotalReceived,
                PendingAmount = x.PendingAmount,
                PercentageOfCurrencySales = denominator <= 0m
                    ? 0m
                    : Math.Round((x.TotalSales / denominator) * 100m, 2)
            };
        }).ToList();

        return new SalesByVesselReportDto
        {
            Meta = BuildMeta(context),
            TotalTransactions = rows.Sum(x => x.TransactionCount),
            TotalSales = SumCurrencyRows(rows.Select(x => (x.Currency, x.TotalSales))),
            TotalReceived = SumCurrencyRows(rows.Select(x => (x.Currency, x.TotalReceived))),
            TotalPending = SumCurrencyRows(rows.Select(x => (x.Currency, x.PendingAmount))),
            Rows = rows
        };
    }

    public async Task<ReportExportResultDto> ExportExcelAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ToFilterQuery(request);

        return request.ReportType switch
        {
            ReportType.SalesSummary => await ExportSalesSummaryExcelAsync(query, cancellationToken),
            ReportType.SalesTransactions => await ExportSalesTransactionsExcelAsync(query, cancellationToken),
            ReportType.SalesByVessel => await ExportSalesByVesselExcelAsync(query, cancellationToken),
            _ => throw new AppException("Unsupported report type.")
        };
    }

    public async Task<ReportExportResultDto> ExportPdfAsync(
        ReportExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ToFilterQuery(request);
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var companyInfo = BuildCompanyInfo(settings);

        return request.ReportType switch
        {
            ReportType.SalesSummary => await ExportSalesSummaryPdfAsync(query, settings.CompanyName, companyInfo, settings.LogoUrl, cancellationToken),
            ReportType.SalesTransactions => await ExportSalesTransactionsPdfAsync(query, settings.CompanyName, companyInfo, settings.LogoUrl, cancellationToken),
            ReportType.SalesByVessel => await ExportSalesByVesselPdfAsync(query, settings.CompanyName, companyInfo, settings.LogoUrl, cancellationToken),
            _ => throw new AppException("Unsupported report type.")
        };
    }

    private async Task<ReportExportResultDto> ExportSalesSummaryExcelAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var report = await GetSalesSummaryAsync(query, cancellationToken);
        var bytes = BuildSalesSummaryExcel(report, settings.IsTaxApplicable);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesSummary, "xlsx"),
            ContentType = ExcelContentType,
            Content = bytes
        };
    }

    private async Task<ReportExportResultDto> ExportSalesTransactionsExcelAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var report = await GetSalesTransactionsAsync(query, cancellationToken);
        var bytes = BuildSalesTransactionsExcel(report);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesTransactions, "xlsx"),
            ContentType = ExcelContentType,
            Content = bytes
        };
    }

    private async Task<ReportExportResultDto> ExportSalesByVesselExcelAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var report = await GetSalesByVesselAsync(query, cancellationToken);
        var bytes = BuildSalesByVesselExcel(report);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesByVessel, "xlsx"),
            ContentType = ExcelContentType,
            Content = bytes
        };
    }

    private async Task<ReportExportResultDto> ExportSalesSummaryPdfAsync(
        ReportFilterQuery query,
        string companyName,
        string companyInfo,
        string? logoUrl,
        CancellationToken cancellationToken)
    {
        var settings = await GetTenantSettingsAsync(cancellationToken);
        var report = await GetSalesSummaryAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesSummaryReportPdf(report, companyName, companyInfo, logoUrl, settings.IsTaxApplicable);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesSummary, "pdf"),
            ContentType = PdfContentType,
            Content = bytes
        };
    }

    private async Task<ReportExportResultDto> ExportSalesTransactionsPdfAsync(
        ReportFilterQuery query,
        string companyName,
        string companyInfo,
        string? logoUrl,
        CancellationToken cancellationToken)
    {
        var report = await GetSalesTransactionsAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesTransactionsReportPdf(report, companyName, companyInfo, logoUrl);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesTransactions, "pdf"),
            ContentType = PdfContentType,
            Content = bytes
        };
    }

    private async Task<ReportExportResultDto> ExportSalesByVesselPdfAsync(
        ReportFilterQuery query,
        string companyName,
        string companyInfo,
        string? logoUrl,
        CancellationToken cancellationToken)
    {
        var report = await GetSalesByVesselAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesByVesselReportPdf(report, companyName, companyInfo, logoUrl);

        return new ReportExportResultDto
        {
            FileName = BuildFileName(ReportType.SalesByVessel, "pdf"),
            ContentType = PdfContentType,
            Content = bytes
        };
    }

    private async Task<ReportQueryContext> BuildContextAsync(ReportFilterQuery query, CancellationToken cancellationToken)
    {
        var range = ResolveRange(query.DatePreset, query.CustomStartDate, query.CustomEndDate);

        string customerFilterLabel;
        if (query.CustomerId.HasValue)
        {
            var customer = await _dbContext.Customers
                .AsNoTracking()
                .Where(x => x.Id == query.CustomerId.Value)
                .Select(x => new { x.Name })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Customer not found.");

            customerFilterLabel = customer.Name;
        }
        else
        {
            customerFilterLabel = AllCustomersLabel;
        }

        return new ReportQueryContext(
            query.DatePreset,
            query.CustomerId,
            customerFilterLabel,
            range);
    }

    private IQueryable<Invoice> BuildInvoiceQuery(ReportQueryContext context)
    {
        var query = _dbContext.Invoices.AsNoTracking();

        if (context.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == context.CustomerId.Value);
        }

        if (context.Range.UseCreatedAtFilter)
        {
            // CreatedAt filtering is reserved for timestamp-based presets.
            query = query.Where(x => x.CreatedAt >= context.Range.StartUtc && x.CreatedAt <= context.Range.EndUtc);
        }
        else
        {
            query = query.Where(x => x.DateIssued >= context.Range.StartDate && x.DateIssued <= context.Range.EndDate);
        }

        return query;
    }

    private static ReportMetaDto BuildMeta(ReportQueryContext context)
    {
        return new ReportMetaDto
        {
            DatePreset = context.DatePreset,
            RangeStartUtc = context.Range.StartUtc,
            RangeEndUtc = context.Range.EndUtc,
            CustomerFilterLabel = context.CustomerFilterLabel,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static ReportFilterQuery ToFilterQuery(ReportExportRequest request)
    {
        return new ReportFilterQuery
        {
            DatePreset = request.DatePreset,
            CustomStartDate = request.CustomStartDate,
            CustomEndDate = request.CustomEndDate,
            CustomerId = request.CustomerId
        };
    }

    private static ReportRange ResolveRange(
        ReportDatePreset preset,
        DateOnly? customStartDate,
        DateOnly? customEndDate)
    {
        var maldivesNow = ToMaldivesTime(DateTimeOffset.UtcNow);
        var today = DateOnly.FromDateTime(maldivesNow.DateTime);

        return preset switch
        {
            ReportDatePreset.Today => BuildDateBasedRange(today, today),
            ReportDatePreset.Yesterday => BuildDateBasedRange(today.AddDays(-1), today.AddDays(-1)),
            ReportDatePreset.Last7Days => BuildDateBasedRange(today.AddDays(-6), today),
            ReportDatePreset.LastWeek => BuildLastWeekRange(today),
            ReportDatePreset.Last30Days => BuildLast30DaysRange(today),
            ReportDatePreset.LastMonth => BuildLastMonthRange(today),
            ReportDatePreset.ThisYear => BuildDateBasedRange(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            ReportDatePreset.CustomRange => BuildCustomRange(customStartDate, customEndDate),
            _ => throw new AppException("Date preset is invalid.")
        };
    }

    private static ReportRange BuildLastWeekRange(DateOnly today)
    {
        var currentWeekStart = StartOfWeek(today, DayOfWeek.Sunday);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var previousWeekEnd = currentWeekStart.AddDays(-1);

        return BuildDateBasedRange(previousWeekStart, previousWeekEnd);
    }

    private static ReportRange BuildLastMonthRange(DateOnly today)
    {
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEnd = currentMonthStart.AddDays(-1);

        return BuildDateBasedRange(previousMonthStart, previousMonthEnd);
    }

    private static ReportRange BuildLast30DaysRange(DateOnly today)
    {
        return BuildDateBasedRange(today.AddDays(-29), today);
    }

    private static ReportRange BuildCustomRange(DateOnly? customStartDate, DateOnly? customEndDate)
    {
        if (!customStartDate.HasValue || !customEndDate.HasValue)
        {
            throw new AppException("Custom range requires both start and end dates.");
        }

        if (customEndDate.Value < customStartDate.Value)
        {
            throw new AppException("Custom range end date cannot be earlier than start date.");
        }

        var rangeDays = customEndDate.Value.DayNumber - customStartDate.Value.DayNumber + 1;
        if (rangeDays > 31)
        {
            throw new AppException("Custom range cannot exceed 31 days.");
        }

        return BuildDateBasedRange(customStartDate.Value, customEndDate.Value);
    }

    private static ReportRange BuildDateBasedRange(DateOnly startDate, DateOnly endDate)
    {
        var startUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), MaldivesOffset).ToUniversalTime();
        var endUtc = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), MaldivesOffset).ToUniversalTime();

        return new ReportRange(startDate, endDate, startUtc, endUtc, false);
    }

    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek firstDayOfWeek)
    {
        var offset = (7 + (date.DayOfWeek - firstDayOfWeek)) % 7;
        return date.AddDays(-offset);
    }

    private static byte[] BuildSalesSummaryExcel(SalesSummaryReportDto report, bool isTaxApplicable)
    {
        using var workbook = CreateReportWorkbook();
        var sheet = workbook.Worksheets.Add("Sales Summary");

        var row = WriteWorkbookHeader(
            sheet,
            "Sales Summary Report",
            isTaxApplicable
                ? "Daily billing performance, receipt capture, pending balances, and tax visibility."
                : "Daily billing performance, receipt capture, and pending balances without tax.",
            report.Meta,
            8);

        var finalSummaryTile = isTaxApplicable
            ? new ExcelSummaryTile("Tax Total", $"{report.TotalTax.Mvr:N2} MVR / {report.TotalTax.Usd:N2} USD", "Reported tax across both currencies.", "#FFF8EE")
            : new ExcelSummaryTile("Coverage Days", report.Rows.Count.ToString("N0"), "Daily rows represented in the selected range.", "#FFF8EE");

        row = WriteSummaryTiles(
            sheet,
            row,
            8,
            [
                new ExcelSummaryTile("Total Invoices", report.TotalInvoices.ToString("N0"), "Invoices issued in selected period.", "#EEF3FF"),
                new ExcelSummaryTile("Total Customers", report.TotalCustomers.ToString("N0"), "Distinct billed customers in range.", "#ECFAF6"),
                new ExcelSummaryTile("Sales (MVR)", report.TotalSales.Mvr.ToString("N2"), "Gross billed in MVR.", "#F4F1FF"),
                new ExcelSummaryTile("Sales (USD)", report.TotalSales.Usd.ToString("N2"), "Gross billed in USD.", "#EFF8FF"),
                new ExcelSummaryTile("Received (MVR)", report.TotalReceived.Mvr.ToString("N2"), "Collections captured in MVR.", "#F3FBF7"),
                new ExcelSummaryTile("Pending (MVR)", report.TotalPending.Mvr.ToString("N2"), "Open MVR receivables.", "#FFF4F7"),
                new ExcelSummaryTile("Received (USD)", report.TotalReceived.Usd.ToString("N2"), "Collections captured in USD.", "#EEF9FF"),
                finalSummaryTile
            ]);

        row = WriteSectionHeading(sheet, row, 1, 8, "Daily sales breakdown");
        var headerRow = row + 1;

        sheet.Cell(headerRow, 1).Value = "Date";
        sheet.Cell(headerRow, 2).Value = "Invoices";
        sheet.Cell(headerRow, 3).Value = "Sales (MVR)";
        sheet.Cell(headerRow, 4).Value = "Sales (USD)";
        sheet.Cell(headerRow, 5).Value = "Received (MVR)";
        sheet.Cell(headerRow, 6).Value = "Received (USD)";
        sheet.Cell(headerRow, 7).Value = "Pending (MVR)";
        sheet.Cell(headerRow, 8).Value = "Pending (USD)";
        StyleExcelTableHeader(sheet.Range(headerRow, 1, headerRow, 8));

        var dataStartRow = headerRow + 1;
        var rowIndex = dataStartRow;
        foreach (var item in report.Rows)
        {
            sheet.Cell(rowIndex, 1).Value = item.Date.ToString("yyyy-MM-dd");
            sheet.Cell(rowIndex, 2).Value = item.InvoiceCount;
            sheet.Cell(rowIndex, 3).Value = item.SalesMvr;
            sheet.Cell(rowIndex, 4).Value = item.SalesUsd;
            sheet.Cell(rowIndex, 5).Value = item.ReceivedMvr;
            sheet.Cell(rowIndex, 6).Value = item.ReceivedUsd;
            sheet.Cell(rowIndex, 7).Value = item.PendingMvr;
            sheet.Cell(rowIndex, 8).Value = item.PendingUsd;
            rowIndex++;
        }

        var totalRow = rowIndex;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 2).Value = report.TotalInvoices;
        sheet.Cell(totalRow, 3).Value = report.TotalSales.Mvr;
        sheet.Cell(totalRow, 4).Value = report.TotalSales.Usd;
        sheet.Cell(totalRow, 5).Value = report.TotalReceived.Mvr;
        sheet.Cell(totalRow, 6).Value = report.TotalReceived.Usd;
        sheet.Cell(totalRow, 7).Value = report.TotalPending.Mvr;
        sheet.Cell(totalRow, 8).Value = report.TotalPending.Usd;

        if (report.Rows.Count > 0)
        {
            StyleExcelBodyRows(sheet.Range(dataStartRow, 1, totalRow - 1, 8));
            sheet.Range(headerRow, 1, totalRow - 1, 8).SetAutoFilter();
        }

        StyleExcelTotalRow(sheet.Range(totalRow, 1, totalRow, 8));
        sheet.Range(dataStartRow, 2, totalRow, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(dataStartRow, 3, totalRow, 8).Style.NumberFormat.Format = "#,##0.00";

        sheet.Column(1).Width = 14;
        sheet.Column(2).Width = 12;
        sheet.Columns(3, 8).Width = 16;
        FinalizeReportSheet(sheet, headerRow, totalRow, 8);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSalesTransactionsExcel(SalesTransactionsReportDto report)
    {
        using var workbook = CreateReportWorkbook();
        var sheet = workbook.Worksheets.Add("Sales Transactions");

        var row = WriteWorkbookHeader(
            sheet,
            "Sales Transactions Report",
            "Invoice-level sales activity with vessel context, payment status, and balance exposure.",
            report.Meta,
            11);

        row = WriteSummaryTiles(
            sheet,
            row,
            11,
            [
                new ExcelSummaryTile("Transactions", report.TotalTransactions.ToString("N0"), "Individual invoice rows in the current export.", "#EEF3FF"),
                new ExcelSummaryTile("Sales (MVR)", report.TotalSales.Mvr.ToString("N2"), "Gross sales booked in MVR.", "#ECFAF6"),
                new ExcelSummaryTile("Sales (USD)", report.TotalSales.Usd.ToString("N2"), "Gross sales booked in USD.", "#EFF8FF"),
                new ExcelSummaryTile("Pending (MVR)", report.TotalPending.Mvr.ToString("N2"), "Outstanding MVR balances.", "#FFF4F7"),
                new ExcelSummaryTile("Pending (USD)", report.TotalPending.Usd.ToString("N2"), "Outstanding USD balances.", "#FFF8EE"),
                new ExcelSummaryTile("Received (MVR)", report.TotalReceived.Mvr.ToString("N2"), "Recorded MVR collections.", "#F3FBF7"),
                new ExcelSummaryTile("Received (USD)", report.TotalReceived.Usd.ToString("N2"), "Recorded USD collections.", "#EEF9FF"),
                new ExcelSummaryTile("Balance View", $"{report.TotalPending.Mvr:N2} / {report.TotalPending.Usd:N2}", "MVR and USD pending totals side by side.", "#F4F1FF")
            ]);

        row = WriteSectionHeading(sheet, row, 1, 11, "Transaction register");
        var headerRow = row + 1;

        sheet.Cell(headerRow, 1).Value = "Invoice No";
        sheet.Cell(headerRow, 2).Value = "Date Issued";
        sheet.Cell(headerRow, 3).Value = "Customer";
        sheet.Cell(headerRow, 4).Value = "Vessel";
        sheet.Cell(headerRow, 5).Value = "Description";
        sheet.Cell(headerRow, 6).Value = "Currency";
        sheet.Cell(headerRow, 7).Value = "Amount";
        sheet.Cell(headerRow, 8).Value = "Payment Status";
        sheet.Cell(headerRow, 9).Value = "Payment Method";
        sheet.Cell(headerRow, 10).Value = "Received On";
        sheet.Cell(headerRow, 11).Value = "Balance";
        StyleExcelTableHeader(sheet.Range(headerRow, 1, headerRow, 11));

        var dataStartRow = headerRow + 1;
        var rowIndex = dataStartRow;
        foreach (var item in report.Rows)
        {
            sheet.Cell(rowIndex, 1).Value = item.InvoiceNo;
            sheet.Cell(rowIndex, 2).Value = item.DateIssued.ToString("yyyy-MM-dd");
            sheet.Cell(rowIndex, 3).Value = item.Customer;
            sheet.Cell(rowIndex, 4).Value = item.Vessel;
            sheet.Cell(rowIndex, 5).Value = item.Description;
            sheet.Cell(rowIndex, 6).Value = item.Currency;
            sheet.Cell(rowIndex, 7).Value = item.Amount;
            sheet.Cell(rowIndex, 8).Value = item.PaymentStatus;
            sheet.Cell(rowIndex, 9).Value = item.PaymentMethod;
            sheet.Cell(rowIndex, 10).Value = item.ReceivedOn?.ToString("yyyy-MM-dd") ?? "-";
            sheet.Cell(rowIndex, 11).Value = item.Balance;
            rowIndex++;
        }

        var totalRow = rowIndex;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 7).Value = $"{report.TotalSales.Mvr:N2} MVR / {report.TotalSales.Usd:N2} USD";
        sheet.Cell(totalRow, 11).Value = $"{report.TotalPending.Mvr:N2} MVR / {report.TotalPending.Usd:N2} USD";

        if (report.Rows.Count > 0)
        {
            StyleExcelBodyRows(sheet.Range(dataStartRow, 1, totalRow - 1, 11));
            sheet.Range(headerRow, 1, totalRow - 1, 11).SetAutoFilter();

            for (var statusRow = dataStartRow; statusRow < totalRow; statusRow++)
            {
                StylePaymentStatusCell(sheet.Cell(statusRow, 8), sheet.Cell(statusRow, 8).GetString());
            }
        }

        StyleExcelTotalRow(sheet.Range(totalRow, 1, totalRow, 11));
        if (totalRow > dataStartRow)
        {
            sheet.Range(dataStartRow, 7, totalRow - 1, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Range(dataStartRow, 11, totalRow - 1, 11).Style.NumberFormat.Format = "#,##0.00";
        }

        sheet.Column(1).Width = 17;
        sheet.Column(2).Width = 13;
        sheet.Column(3).Width = 20;
        sheet.Column(4).Width = 18;
        sheet.Column(5).Width = 40;
        sheet.Column(6).Width = 11;
        sheet.Column(7).Width = 14;
        sheet.Column(8).Width = 15;
        sheet.Column(9).Width = 15;
        sheet.Column(10).Width = 13;
        sheet.Column(11).Width = 14;
        FinalizeReportSheet(sheet, headerRow, totalRow, 11);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSalesByVesselExcel(SalesByVesselReportDto report)
    {
        using var workbook = CreateReportWorkbook();
        var sheet = workbook.Worksheets.Add("Sales By Vessel");
        var leadMvr = report.Rows
            .Where(x => string.Equals(x.Currency, "MVR", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TotalSales)
            .FirstOrDefault();
        var leadUsd = report.Rows
            .Where(x => string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TotalSales)
            .FirstOrDefault();

        var row = WriteWorkbookHeader(
            sheet,
            "Sales By Vessel Report",
            "Contribution, receipts, and pending balances grouped by vessel and currency.",
            report.Meta,
            7);

        row = WriteSummaryTiles(
            sheet,
            row,
            7,
            [
                new ExcelSummaryTile("Transactions", report.TotalTransactions.ToString("N0"), "All transactions in this vessel export.", "#EEF3FF"),
                new ExcelSummaryTile("Sales (MVR)", report.TotalSales.Mvr.ToString("N2"), leadMvr is null ? "No MVR vessel sales in range." : $"Lead MVR vessel: {leadMvr.Vessel}", "#ECFAF6"),
                new ExcelSummaryTile("Sales (USD)", report.TotalSales.Usd.ToString("N2"), leadUsd is null ? "No USD vessel sales in range." : $"Lead USD vessel: {leadUsd.Vessel}", "#EFF8FF"),
                new ExcelSummaryTile("Pending", $"{report.TotalPending.Mvr:N2} / {report.TotalPending.Usd:N2}", "Open MVR and USD balances by vessel mix.", "#FFF4F7")
            ]);

        row = WriteSectionHeading(sheet, row, 1, 7, "Vessel contribution table");
        var headerRow = row + 1;

        sheet.Cell(headerRow, 1).Value = "Vessel";
        sheet.Cell(headerRow, 2).Value = "Currency";
        sheet.Cell(headerRow, 3).Value = "Transactions";
        sheet.Cell(headerRow, 4).Value = "Total Sales";
        sheet.Cell(headerRow, 5).Value = "Total Received";
        sheet.Cell(headerRow, 6).Value = "Pending";
        sheet.Cell(headerRow, 7).Value = "% of Currency Sales";
        StyleExcelTableHeader(sheet.Range(headerRow, 1, headerRow, 7));

        var dataStartRow = headerRow + 1;
        var rowIndex = dataStartRow;
        foreach (var item in report.Rows)
        {
            sheet.Cell(rowIndex, 1).Value = item.Vessel;
            sheet.Cell(rowIndex, 2).Value = item.Currency;
            sheet.Cell(rowIndex, 3).Value = item.TransactionCount;
            sheet.Cell(rowIndex, 4).Value = item.TotalSales;
            sheet.Cell(rowIndex, 5).Value = item.TotalReceived;
            sheet.Cell(rowIndex, 6).Value = item.PendingAmount;
            sheet.Cell(rowIndex, 7).Value = item.PercentageOfCurrencySales / 100m;
            rowIndex++;
        }

        var totalRow = rowIndex;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 2).Value = "-";
        sheet.Cell(totalRow, 3).Value = report.TotalTransactions;
        sheet.Cell(totalRow, 4).Value = $"{report.TotalSales.Mvr:N2} / {report.TotalSales.Usd:N2}";
        sheet.Cell(totalRow, 5).Value = $"{report.TotalReceived.Mvr:N2} / {report.TotalReceived.Usd:N2}";
        sheet.Cell(totalRow, 6).Value = $"{report.TotalPending.Mvr:N2} / {report.TotalPending.Usd:N2}";
        sheet.Cell(totalRow, 7).Value = "-";

        if (report.Rows.Count > 0)
        {
            StyleExcelBodyRows(sheet.Range(dataStartRow, 1, totalRow - 1, 7));
            sheet.Range(headerRow, 1, totalRow - 1, 7).SetAutoFilter();
        }

        StyleExcelTotalRow(sheet.Range(totalRow, 1, totalRow, 7));
        if (totalRow > dataStartRow)
        {
            sheet.Range(dataStartRow, 4, totalRow - 1, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Range(dataStartRow, 7, totalRow - 1, 7).Style.NumberFormat.Format = "0.00%";
            sheet.Range(dataStartRow, 3, totalRow - 1, 3).Style.NumberFormat.Format = "#,##0";
        }

        sheet.Column(1).Width = 28;
        sheet.Column(2).Width = 11;
        sheet.Column(3).Width = 13;
        sheet.Columns(4, 6).Width = 16;
        sheet.Column(7).Width = 18;
        FinalizeReportSheet(sheet, headerRow, totalRow, 7);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLWorkbook CreateReportWorkbook()
    {
        var workbook = new XLWorkbook();
        workbook.Author = "myDhathuru";
        workbook.Style.Font.FontName = "Aptos";
        workbook.Style.Font.FontSize = 10;
        workbook.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        return workbook;
    }

    private static int WriteWorkbookHeader(
        IXLWorksheet sheet,
        string title,
        string subtitle,
        ReportMetaDto meta,
        int totalColumns)
    {
        sheet.PageSetup.ShowGridlines = false;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.CenterHorizontally = true;

        var heroRange = sheet.Range(1, 1, 3, totalColumns);
        heroRange.Merge();
        heroRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#18315D");
        heroRange.Style.Font.FontColor = XLColor.White;
        heroRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        heroRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        heroRange.Style.Alignment.WrapText = true;

        var hero = sheet.Cell(1, 1).GetRichText();
        hero.ClearText();
        hero.AddText(title).SetBold().SetFontSize(19).SetFontColor(XLColor.White);
        hero.AddText(Environment.NewLine);
        hero.AddText(subtitle).SetFontSize(10).SetFontColor(XLColor.FromHtml("#D7E6FF"));
        hero.AddText(Environment.NewLine);
        hero.AddText($"Generated {ToMaldivesTime(meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT").SetFontSize(9).SetFontColor(XLColor.FromHtml("#BFD4FF"));

        sheet.Row(1).Height = 26;
        sheet.Row(2).Height = 22;
        sheet.Row(3).Height = 22;

        var metaCards = new[]
        {
            new ExcelMetaCard("Preset", meta.DatePreset.ToString()),
            new ExcelMetaCard("Customer Filter", meta.CustomerFilterLabel),
            new ExcelMetaCard("Range Start", ToMaldivesTime(meta.RangeStartUtc).ToString("yyyy-MM-dd HH:mm")),
            new ExcelMetaCard("Range End", ToMaldivesTime(meta.RangeEndUtc).ToString("yyyy-MM-dd HH:mm"))
        };

        var metaSegments = BuildColumnSegments(totalColumns, metaCards.Length);
        for (var index = 0; index < metaCards.Length; index++)
        {
            var (startCol, endCol) = metaSegments[index];
            WriteMetaCard(sheet, 5, 6, startCol, endCol, metaCards[index]);
        }

        return 8;
    }

    private static int WriteSummaryTiles(
        IXLWorksheet sheet,
        int startRow,
        int totalColumns,
        IReadOnlyList<ExcelSummaryTile> tiles)
    {
        var currentRow = startRow;

        foreach (var tileGroup in tiles.Chunk(4))
        {
            var segments = BuildColumnSegments(totalColumns, tileGroup.Length);
            for (var index = 0; index < tileGroup.Length; index++)
            {
                var (startCol, endCol) = segments[index];
                WriteSummaryTile(sheet, currentRow, currentRow + 2, startCol, endCol, tileGroup[index]);
            }

            currentRow += 4;
        }

        return currentRow;
    }

    private static int WriteSectionHeading(IXLWorksheet sheet, int row, int startColumn, int endColumn, string title)
    {
        var range = sheet.Range(row, startColumn, row, endColumn);
        range.Merge();
        range.Value = title;
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");
        range.Style.Font.FontSize = 11;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF3FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.Indent = 1;
        sheet.Row(row).Height = 22;
        return row;
    }

    private static void WriteMetaCard(
        IXLWorksheet sheet,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        ExcelMetaCard card)
    {
        var range = sheet.Range(startRow, startColumn, endRow, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7FAFF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.SetIndent(1);

        var rich = sheet.Cell(startRow, startColumn).GetRichText();
        rich.ClearText();
        rich.AddText(card.Label.ToUpperInvariant()).SetBold().SetFontSize(8).SetFontColor(XLColor.FromHtml("#6A7FA8"));
        rich.AddText(Environment.NewLine);
        rich.AddText(card.Value).SetBold().SetFontSize(10.5).SetFontColor(XLColor.FromHtml("#243B63"));
    }

    private static void WriteSummaryTile(
        IXLWorksheet sheet,
        int startRow,
        int endRow,
        int startColumn,
        int endColumn,
        ExcelSummaryTile tile)
    {
        var range = sheet.Range(startRow, startColumn, endRow, endColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(tile.FillColor);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        range.Style.Alignment.SetIndent(1);

        var cell = sheet.Cell(startRow, startColumn);
        var rich = cell.GetRichText();
        rich.ClearText();
        rich.AddText(tile.Label.ToUpperInvariant()).SetBold().SetFontSize(8).SetFontColor(XLColor.FromHtml("#6A7FA8"));
        rich.AddText(Environment.NewLine);
        rich.AddText(tile.Value).SetBold().SetFontSize(15).SetFontColor(XLColor.FromHtml("#243B63"));
        rich.AddText(Environment.NewLine);
        rich.AddText(tile.Detail).SetFontSize(8.5).SetFontColor(XLColor.FromHtml("#6178A3"));
    }

    private static IReadOnlyList<(int StartColumn, int EndColumn)> BuildColumnSegments(int totalColumns, int segmentCount)
    {
        var segments = new List<(int StartColumn, int EndColumn)>(segmentCount);
        var baseWidth = totalColumns / segmentCount;
        var remainder = totalColumns % segmentCount;
        var cursor = 1;

        for (var index = 0; index < segmentCount; index++)
        {
            var width = baseWidth + (index < remainder ? 1 : 0);
            var startColumn = cursor;
            var endColumn = cursor + width - 1;
            segments.Add((startColumn, endColumn));
            cursor = endColumn + 1;
        }

        return segments;
    }

    private static void StyleExcelTableHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleExcelBodyRows(IXLRange range)
    {
        if (range.RowCount() <= 0)
        {
            return;
        }

        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.WrapText = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#30466F");

        for (var index = 1; index <= range.RowCount(); index++)
        {
            var rowRange = range.Row(index);
            rowRange.Style.Fill.BackgroundColor = index % 2 == 0
                ? XLColor.FromHtml("#F9FBFF")
                : XLColor.White;
        }
    }

    private static void StyleExcelTotalRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml("#243B63");
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDF3FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D8E2F4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D8E2F4");
    }

    private static void StylePaymentStatusCell(IXLCell cell, string status)
    {
        var normalized = status.Trim();
        var fill = normalized switch
        {
            "Paid" => "#DCF6E8",
            "Partial" => "#FFF1D8",
            _ => "#FFE7EF"
        };

        var text = normalized switch
        {
            "Paid" => "#0F8B57",
            "Partial" => "#B46A00",
            _ => "#B33E63"
        };

        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(fill);
        cell.Style.Font.FontColor = XLColor.FromHtml(text);
        cell.Style.Font.Bold = true;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void FinalizeReportSheet(IXLWorksheet sheet, int headerRow, int lastRow, int totalColumns)
    {
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Range(1, 1, lastRow, totalColumns).Style.Font.FontName = "Aptos";
        sheet.Rows(1, lastRow).AdjustToContents();
        sheet.Range(1, 1, lastRow, totalColumns).Style.Alignment.WrapText = true;
    }

    private sealed record ExcelMetaCard(string Label, string Value);

    private sealed record ExcelSummaryTile(string Label, string Value, string Detail, string FillColor);

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string BuildCompanyInfo(TenantSettings settings)
    {
        return settings.BuildCompanyInfo(includeBusinessRegistration: true);
    }

    private static string BuildFileName(ReportType reportType, string extension)
    {
        var prefix = reportType switch
        {
            ReportType.SalesSummary => "sales-summary",
            ReportType.SalesTransactions => "sales-transactions",
            ReportType.SalesByVessel => "sales-by-vessel",
            _ => "report"
        };

        return $"{prefix}-{ToMaldivesTime(DateTimeOffset.UtcNow):yyyy-MM-dd}.{extension}";
    }

    private static CurrencyTotalsDto SumCurrencyRows(IEnumerable<(string Currency, decimal Amount)> rows)
    {
        var totals = new CurrencyTotalsDto();
        foreach (var row in rows)
        {
            var currency = NormalizeCurrency(row.Currency);
            if (currency == "USD")
            {
                totals.Usd += row.Amount;
            }
            else
            {
                totals.Mvr += row.Amount;
            }
        }

        totals.Mvr = Math.Round(totals.Mvr, 2, MidpointRounding.AwayFromZero);
        totals.Usd = Math.Round(totals.Usd, 2, MidpointRounding.AwayFromZero);
        return totals;
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.Equals(currency?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
        {
            return "USD";
        }

        return "MVR";
    }

    private static DateTimeOffset ToMaldivesTime(DateTimeOffset utcDateTime)
    {
        return utcDateTime.ToOffset(MaldivesOffset);
    }

    private sealed record ReportQueryContext(
        ReportDatePreset DatePreset,
        Guid? CustomerId,
        string CustomerFilterLabel,
        ReportRange Range);

    private sealed record ReportRange(
        DateOnly StartDate,
        DateOnly EndDate,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        bool UseCreatedAtFilter);
}

