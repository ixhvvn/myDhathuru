using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Domain.Entities;
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
            ReportType.SalesSummary => await ExportSalesSummaryPdfAsync(query, settings.CompanyName, companyInfo, cancellationToken),
            ReportType.SalesTransactions => await ExportSalesTransactionsPdfAsync(query, settings.CompanyName, companyInfo, cancellationToken),
            ReportType.SalesByVessel => await ExportSalesByVesselPdfAsync(query, settings.CompanyName, companyInfo, cancellationToken),
            _ => throw new AppException("Unsupported report type.")
        };
    }

    private async Task<ReportExportResultDto> ExportSalesSummaryExcelAsync(
        ReportFilterQuery query,
        CancellationToken cancellationToken)
    {
        var report = await GetSalesSummaryAsync(query, cancellationToken);
        var bytes = BuildSalesSummaryExcel(report);

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
        CancellationToken cancellationToken)
    {
        var report = await GetSalesSummaryAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesSummaryReportPdf(report, companyName, companyInfo);

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
        CancellationToken cancellationToken)
    {
        var report = await GetSalesTransactionsAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesTransactionsReportPdf(report, companyName, companyInfo);

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
        CancellationToken cancellationToken)
    {
        var report = await GetSalesByVesselAsync(query, cancellationToken);
        var bytes = _pdfExportService.BuildSalesByVesselReportPdf(report, companyName, companyInfo);

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

    private static byte[] BuildSalesSummaryExcel(SalesSummaryReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sales Summary");

        var tableStartRow = WriteHeader(sheet, "Sales Summary Report", report.Meta, 8);

        var metricRow = tableStartRow;
        sheet.Cell(metricRow, 1).Value = "Total Invoices";
        sheet.Cell(metricRow, 2).Value = report.TotalInvoices;
        sheet.Cell(metricRow, 3).Value = "Total Customers";
        sheet.Cell(metricRow, 4).Value = report.TotalCustomers;
        StyleMetricHeader(sheet.Range(metricRow, 1, metricRow, 4));

        var currencyMetricRow = metricRow + 1;
        sheet.Cell(currencyMetricRow, 1).Value = "Metric";
        sheet.Cell(currencyMetricRow, 2).Value = "MVR";
        sheet.Cell(currencyMetricRow, 3).Value = "USD";
        StyleTableHeader(sheet.Range(currencyMetricRow, 1, currencyMetricRow, 3));

        var metricDataRow = currencyMetricRow + 1;
        WriteCurrencyMetric(sheet, metricDataRow++, "Total Sales", report.TotalSales);
        WriteCurrencyMetric(sheet, metricDataRow++, "Total Received", report.TotalReceived);
        WriteCurrencyMetric(sheet, metricDataRow++, "Total Pending", report.TotalPending);
        WriteCurrencyMetric(sheet, metricDataRow, "Total Tax", report.TotalTax);
        StyleDataBorder(sheet.Range(currencyMetricRow + 1, 1, metricDataRow, 3));
        sheet.Range(currencyMetricRow + 1, 2, metricDataRow, 3).Style.NumberFormat.Format = "#,##0.00";

        var headerRow = metricDataRow + 2;
        sheet.Cell(headerRow, 1).Value = "Date";
        sheet.Cell(headerRow, 2).Value = "No. of Invoices";
        sheet.Cell(headerRow, 3).Value = "Sales (MVR)";
        sheet.Cell(headerRow, 4).Value = "Sales (USD)";
        sheet.Cell(headerRow, 5).Value = "Received (MVR)";
        sheet.Cell(headerRow, 6).Value = "Received (USD)";
        sheet.Cell(headerRow, 7).Value = "Pending (MVR)";
        sheet.Cell(headerRow, 8).Value = "Pending (USD)";
        StyleTableHeader(sheet.Range(headerRow, 1, headerRow, 8));

        var row = headerRow + 1;
        foreach (var item in report.Rows)
        {
            sheet.Cell(row, 1).Value = item.Date.ToString("yyyy-MM-dd");
            sheet.Cell(row, 2).Value = item.InvoiceCount;
            sheet.Cell(row, 3).Value = item.SalesMvr;
            sheet.Cell(row, 4).Value = item.SalesUsd;
            sheet.Cell(row, 5).Value = item.ReceivedMvr;
            sheet.Cell(row, 6).Value = item.ReceivedUsd;
            sheet.Cell(row, 7).Value = item.PendingMvr;
            sheet.Cell(row, 8).Value = item.PendingUsd;
            row++;
        }

        var totalRow = row;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 2).Value = report.TotalInvoices;
        sheet.Cell(totalRow, 3).Value = report.TotalSales.Mvr;
        sheet.Cell(totalRow, 4).Value = report.TotalSales.Usd;
        sheet.Cell(totalRow, 5).Value = report.TotalReceived.Mvr;
        sheet.Cell(totalRow, 6).Value = report.TotalReceived.Usd;
        sheet.Cell(totalRow, 7).Value = report.TotalPending.Mvr;
        sheet.Cell(totalRow, 8).Value = report.TotalPending.Usd;
        StyleTotalsRow(sheet.Range(totalRow, 1, totalRow, 8));

        if (report.Rows.Count > 0)
        {
            StyleDataBorder(sheet.Range(headerRow + 1, 1, totalRow - 1, 8));
            sheet.Range(headerRow, 1, totalRow - 1, 8).SetAutoFilter();
        }

        sheet.Range(headerRow + 1, 3, totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 2, totalRow, 2).Style.NumberFormat.Format = "#,##0";
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns(1, 8).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSalesTransactionsExcel(SalesTransactionsReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sales Transactions");

        var tableStartRow = WriteHeader(sheet, "Sales Transactions Report", report.Meta, 11);

        var metricRow = tableStartRow;
        sheet.Cell(metricRow, 1).Value = "Transactions";
        sheet.Cell(metricRow, 2).Value = report.TotalTransactions;
        StyleMetricHeader(sheet.Range(metricRow, 1, metricRow, 2));

        var totalsHeaderRow = metricRow + 1;
        sheet.Cell(totalsHeaderRow, 1).Value = "Metric";
        sheet.Cell(totalsHeaderRow, 2).Value = "MVR";
        sheet.Cell(totalsHeaderRow, 3).Value = "USD";
        StyleTableHeader(sheet.Range(totalsHeaderRow, 1, totalsHeaderRow, 3));

        var totalsRow = totalsHeaderRow + 1;
        WriteCurrencyMetric(sheet, totalsRow++, "Total Sales", report.TotalSales);
        WriteCurrencyMetric(sheet, totalsRow++, "Total Received", report.TotalReceived);
        WriteCurrencyMetric(sheet, totalsRow, "Total Pending", report.TotalPending);
        StyleDataBorder(sheet.Range(totalsHeaderRow + 1, 1, totalsRow, 3));
        sheet.Range(totalsHeaderRow + 1, 2, totalsRow, 3).Style.NumberFormat.Format = "#,##0.00";

        var headerRow = totalsRow + 2;
        sheet.Cell(headerRow, 1).Value = "Invoice No";
        sheet.Cell(headerRow, 2).Value = "Date Issued";
        sheet.Cell(headerRow, 3).Value = "Customer";
        sheet.Cell(headerRow, 4).Value = "Vessel";
        sheet.Cell(headerRow, 5).Value = "Description / Details";
        sheet.Cell(headerRow, 6).Value = "Currency";
        sheet.Cell(headerRow, 7).Value = "Amount";
        sheet.Cell(headerRow, 8).Value = "Payment Status";
        sheet.Cell(headerRow, 9).Value = "Payment Method";
        sheet.Cell(headerRow, 10).Value = "Received On";
        sheet.Cell(headerRow, 11).Value = "Balance";
        StyleTableHeader(sheet.Range(headerRow, 1, headerRow, 11));

        var row = headerRow + 1;
        foreach (var item in report.Rows)
        {
            sheet.Cell(row, 1).Value = item.InvoiceNo;
            sheet.Cell(row, 2).Value = item.DateIssued.ToString("yyyy-MM-dd");
            sheet.Cell(row, 3).Value = item.Customer;
            sheet.Cell(row, 4).Value = item.Vessel;
            sheet.Cell(row, 5).Value = item.Description;
            sheet.Cell(row, 6).Value = item.Currency;
            sheet.Cell(row, 7).Value = item.Amount;
            sheet.Cell(row, 8).Value = item.PaymentStatus;
            sheet.Cell(row, 9).Value = item.PaymentMethod;
            sheet.Cell(row, 10).Value = item.ReceivedOn?.ToString("yyyy-MM-dd") ?? "-";
            sheet.Cell(row, 11).Value = item.Balance;
            row++;
        }

        var totalRow = row;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 7).Value = $"{report.TotalSales.Mvr:N2} MVR / {report.TotalSales.Usd:N2} USD";
        sheet.Cell(totalRow, 11).Value = $"{report.TotalPending.Mvr:N2} MVR / {report.TotalPending.Usd:N2} USD";
        StyleTotalsRow(sheet.Range(totalRow, 1, totalRow, 11));

        if (report.Rows.Count > 0)
        {
            StyleDataBorder(sheet.Range(headerRow + 1, 1, totalRow - 1, 11));
            sheet.Range(headerRow, 1, totalRow - 1, 11).SetAutoFilter();
        }

        sheet.Range(headerRow + 1, 7, totalRow - 1, 7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 11, totalRow - 1, 11).Style.NumberFormat.Format = "#,##0.00";
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns(1, 11).AdjustToContents();
        sheet.Column(5).Width = Math.Max(36, sheet.Column(5).Width);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildSalesByVesselExcel(SalesByVesselReportDto report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sales By Vessel");

        var tableStartRow = WriteHeader(sheet, "Sales By Vessel Report", report.Meta, 7);

        var metricRow = tableStartRow;
        sheet.Cell(metricRow, 1).Value = "Transactions";
        sheet.Cell(metricRow, 2).Value = report.TotalTransactions;
        StyleMetricHeader(sheet.Range(metricRow, 1, metricRow, 2));

        var totalsHeaderRow = metricRow + 1;
        sheet.Cell(totalsHeaderRow, 1).Value = "Metric";
        sheet.Cell(totalsHeaderRow, 2).Value = "MVR";
        sheet.Cell(totalsHeaderRow, 3).Value = "USD";
        StyleTableHeader(sheet.Range(totalsHeaderRow, 1, totalsHeaderRow, 3));

        var totalsRow = totalsHeaderRow + 1;
        WriteCurrencyMetric(sheet, totalsRow++, "Total Sales", report.TotalSales);
        WriteCurrencyMetric(sheet, totalsRow++, "Total Received", report.TotalReceived);
        WriteCurrencyMetric(sheet, totalsRow, "Total Pending", report.TotalPending);
        StyleDataBorder(sheet.Range(totalsHeaderRow + 1, 1, totalsRow, 3));
        sheet.Range(totalsHeaderRow + 1, 2, totalsRow, 3).Style.NumberFormat.Format = "#,##0.00";

        var headerRow = totalsRow + 2;
        sheet.Cell(headerRow, 1).Value = "Vessel";
        sheet.Cell(headerRow, 2).Value = "Currency";
        sheet.Cell(headerRow, 3).Value = "No. of Transactions";
        sheet.Cell(headerRow, 4).Value = "Total Sales";
        sheet.Cell(headerRow, 5).Value = "Total Received";
        sheet.Cell(headerRow, 6).Value = "Pending Amount";
        sheet.Cell(headerRow, 7).Value = "% of Currency Sales";
        StyleTableHeader(sheet.Range(headerRow, 1, headerRow, 7));

        var row = headerRow + 1;
        foreach (var item in report.Rows)
        {
            sheet.Cell(row, 1).Value = item.Vessel;
            sheet.Cell(row, 2).Value = item.Currency;
            sheet.Cell(row, 3).Value = item.TransactionCount;
            sheet.Cell(row, 4).Value = item.TotalSales;
            sheet.Cell(row, 5).Value = item.TotalReceived;
            sheet.Cell(row, 6).Value = item.PendingAmount;
            sheet.Cell(row, 7).Value = item.PercentageOfCurrencySales / 100m;
            row++;
        }

        var totalRow = row;
        sheet.Cell(totalRow, 1).Value = "TOTAL";
        sheet.Cell(totalRow, 2).Value = "-";
        sheet.Cell(totalRow, 3).Value = report.TotalTransactions;
        sheet.Cell(totalRow, 4).Value = $"{report.TotalSales.Mvr:N2} / {report.TotalSales.Usd:N2}";
        sheet.Cell(totalRow, 5).Value = $"{report.TotalReceived.Mvr:N2} / {report.TotalReceived.Usd:N2}";
        sheet.Cell(totalRow, 6).Value = $"{report.TotalPending.Mvr:N2} / {report.TotalPending.Usd:N2}";
        sheet.Cell(totalRow, 7).Value = "-";
        StyleTotalsRow(sheet.Range(totalRow, 1, totalRow, 7));

        if (report.Rows.Count > 0)
        {
            StyleDataBorder(sheet.Range(headerRow + 1, 1, totalRow - 1, 7));
            sheet.Range(headerRow, 1, totalRow - 1, 7).SetAutoFilter();
        }

        sheet.Range(headerRow + 1, 4, totalRow - 1, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(headerRow + 1, 7, totalRow - 1, 7).Style.NumberFormat.Format = "0.00%";
        sheet.Range(headerRow + 1, 3, totalRow - 1, 3).Style.NumberFormat.Format = "#,##0";
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns(1, 7).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int WriteHeader(IXLWorksheet sheet, string title, ReportMetaDto meta, int totalColumns)
    {
        sheet.Cell(1, 1).Value = title;
        var titleRange = sheet.Range(1, 1, 1, totalColumns);
        titleRange.Merge();
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        sheet.Cell(2, 1).Value = $"Generated (MVT): {ToMaldivesTime(meta.GeneratedAtUtc):yyyy-MM-dd HH:mm}";
        sheet.Cell(3, 1).Value = $"Date Preset: {meta.DatePreset}";
        sheet.Cell(4, 1).Value =
            $"Date Range (MVT): {ToMaldivesTime(meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(meta.RangeEndUtc):yyyy-MM-dd HH:mm}";
        sheet.Cell(5, 1).Value = $"Customer Filter: {meta.CustomerFilterLabel}";

        for (var row = 2; row <= 5; row++)
        {
            var metaRowRange = sheet.Range(row, 1, row, totalColumns);
            metaRowRange.Merge();
            metaRowRange.Style.Font.FontSize = 10;
            metaRowRange.Style.Font.FontColor = XLColor.FromHtml("#51648F");
            metaRowRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        return 7;
    }

    private static void WriteCurrencyMetric(IXLWorksheet sheet, int row, string label, CurrencyTotalsDto totals)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 2).Value = totals.Mvr;
        sheet.Cell(row, 3).Value = totals.Usd;
    }

    private static void StyleMetricHeader(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F6F8FF");
        range.Style.Font.Bold = true;
    }

    private static void StyleTableHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEFF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void StyleTotalsRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5FF");
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void StyleDataBorder(IXLRange range)
    {
        if (range.RowCount() <= 0)
        {
            return;
        }

        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private async Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context missing.");
        return await _dbContext.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant settings not found.");
    }

    private static string BuildCompanyInfo(TenantSettings settings)
    {
        return
            $"{settings.BusinessRegistrationNumber}, TIN: {settings.TinNumber}, Phone: {settings.CompanyPhone}, Email: {settings.CompanyEmail}";
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
