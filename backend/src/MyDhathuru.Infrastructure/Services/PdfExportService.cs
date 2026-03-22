using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Application.PaymentVouchers.Dtos;
using MyDhathuru.Application.PurchaseOrders.Dtos;
using MyDhathuru.Application.Quotations.Dtos;
using MyDhathuru.Application.Statements.Dtos;
using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Application.Expenses.Dtos;
using MyDhathuru.Application.StaffConduct.Dtos;
using MyDhathuru.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyDhathuru.Infrastructure.Services;

public class PdfExportService : IPdfExportService
{
    private static readonly TimeSpan MaldivesOffset = TimeSpan.FromHours(5);
    private const int MaxPortalAdminLogoBytes = 5 * 1024 * 1024;
    private const string FarumaFontFamily = "Faruma";
    private const string DefaultAppLogoFileName = "newlogo.png";
    private const string DefaultInvoiceLogoFileName = "newlogo.png";
    private static readonly HttpClient LogoHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly object FarumaFontSync = new();
    private static bool FarumaFontRegistrationAttempted;
    private static bool FarumaFontRegistered;

    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(IWebHostEnvironment hostEnvironment, ILogger<PdfExportService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    private sealed class LogoAsset
    {
        public byte[]? ImageBytes { get; init; }
        public string? SvgMarkup { get; init; }

        public bool HasValue => (ImageBytes?.Length ?? 0) > 0 || !string.IsNullOrWhiteSpace(SvgMarkup);
    }

    public byte[] BuildDeliveryNotePdf(DeliveryNoteDetailDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        var currency = string.Equals((model.Currency ?? string.Empty).Trim(), "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "MVR";
        var cashPaymentTotal = model.Items.Sum(x => x.CashPayment);
        var vesselPaymentTotal = model.VesselPaymentFee > 0
            ? model.VesselPaymentFee
            : model.Items.Sum(x => x.VesselPayment);
        var recordedPaymentTotal = cashPaymentTotal + vesselPaymentTotal;
        var balanceAmount = Math.Max(model.TotalAmount - recordedPaymentTotal, 0m);
        var documentStatus = !string.IsNullOrWhiteSpace(model.InvoiceNo)
            ? "Invoiced"
            : recordedPaymentTotal >= model.TotalAmount && model.TotalAmount > 0
                ? "Settled"
                : "Open";
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static (string Fill, string Outline, string Text) StatusColors(string status) => status switch
        {
            "Settled" => ("#DCF6E8", "#95D8B0", "#0F8B57"),
            "Invoiced" => ("#E7EDFF", "#AFC1F8", "#4157B2"),
            _ => ("#FFF1D8", "#F2C37E", "#B46A00")
        };

        var statusColors = StatusColors(documentStatus);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Delivery Note").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Delivery Note").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("DELIVERY NOTE").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Element(chip =>
                                        chip.Border(1)
                                            .BorderColor(statusColors.Outline)
                                            .Background(statusColors.Fill)
                                            .CornerRadius(12)
                                            .PaddingHorizontal(10)
                                            .PaddingVertical(4)
                                            .Text(documentStatus)
                                            .SemiBold()
                                            .FontSize(8.8f)
                                            .FontColor(statusColors.Text));
                                    meta.Item().AlignRight().Text($"DN No: {Safe(model.DeliveryNoteNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Date: {model.Date:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Currency: {currency}");
                                });
                        });

                        header.Item().Text(
                                $"Dispatch prepared for {Safe(model.CustomerName)} | Vessel / Courier: {Safe(model.VesselName)} | Linked invoice: {Safe(model.InvoiceNo)}")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(details =>
                                {
                                    details.Spacing(4);
                                    details.Item().Text("Customer Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    details.Item().Text(model.CustomerName).Bold().FontSize(11.2f);
                                    details.Item().Text($"Vessel / Courier: {Safe(model.VesselName)}").FontColor(Muted);
                                    details.Item().Text($"PO Number: {Safe(model.PoNumber)}").FontColor(Muted);
                                    details.Item().Text($"Linked Invoice: {Safe(model.InvoiceNo)}").FontColor(Muted);
                                    if (!string.IsNullOrWhiteSpace(model.VesselPaymentInvoiceNumber))
                                    {
                                        details.Item().Text($"Vessel Payment Invoice: {Safe(model.VesselPaymentInvoiceNumber)}").FontColor(Muted);
                                    }
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(88);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("Items").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.Items.Count.ToString());
                                    table.Cell().Element(LabelCell).Text("Total Qty").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.Items.Sum(x => x.Qty).ToString("N2"));
                                    table.Cell().Element(LabelCell).Text("Cash Paid").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Money(currency, cashPaymentTotal));
                                    table.Cell().Element(LabelCell).Text("Vessel Paid").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Money(currency, vesselPaymentTotal));
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.2f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Document Total", Money(currency, model.TotalAmount)));
                            row.RelativeItem().Element(c => Metric(c, "Items / Qty", $"{model.Items.Count} / {model.Items.Sum(x => x.Qty):N2}"));
                            row.RelativeItem().Element(c => Metric(c, "Recorded Paid", Money(currency, recordedPaymentTotal)));
                            row.RelativeItem().Element(c => Metric(c, "Balance", Money(currency, balanceAmount)));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);
                                columns.RelativeColumn(4.6f);
                                columns.ConstantColumn(74);
                                columns.ConstantColumn(92);
                                columns.ConstantColumn(102);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                            });

                            if (model.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(5)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No delivery note items");
                            }
                            else
                            {
                                for (var i = 0; i < model.Items.Count; i++)
                                {
                                    var item = model.Items[i];
                                    var rowBackground = i % 2 == 0 ? "#FFFFFF" : Panel;

                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).Text(item.Details);
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(item.Qty.ToString("N2"));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Rate));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Total));
                                }
                            }
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Notes").Bold().FontSize(10.4f).FontColor(Ink);
                                    notes.Item().Text(
                                        string.IsNullOrWhiteSpace(model.Notes)
                                            ? "No additional delivery note remarks were added."
                                            : model.Notes!.Trim())
                                        .FontColor(Muted);
                                });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(6);
                                    summary.Item().Text("Summary").Bold().FontSize(10.4f).FontColor(Ink);

                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Document Total").SemiBold().FontColor(Muted);
                                        item.ConstantItem(90).AlignRight().Text(Money(currency, model.TotalAmount)).Bold();
                                    });

                                    if (cashPaymentTotal > 0)
                                    {
                                        summary.Item().Row(item =>
                                        {
                                            item.RelativeItem().Text("Cash Payment").SemiBold().FontColor(Muted);
                                            item.ConstantItem(90).AlignRight().Text(Money(currency, cashPaymentTotal));
                                        });
                                    }

                                    if (vesselPaymentTotal > 0)
                                    {
                                        summary.Item().Row(item =>
                                        {
                                            item.RelativeItem().Text("Vessel Payment").SemiBold().FontColor(Muted);
                                            item.ConstantItem(90).AlignRight().Text(Money(currency, vesselPaymentTotal));
                                        });
                                    }

                                    summary.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Border);

                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Balance").Bold().FontColor(Ink);
                                        item.ConstantItem(90).AlignRight().Text(Money(currency, balanceAmount)).Bold().FontColor(Ink);
                                    });
                                });
                        });
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildInvoicePdf(InvoiceDetailDto model, string companyName, string companyInfo, InvoiceBankDetailsDto bankDetails, string? logoUrl, bool isTaxApplicable)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        var currency = string.Equals((model.Currency ?? string.Empty).Trim(), "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "MVR";

        var dueDays = Math.Max(model.DateDue.DayNumber - model.DateIssued.DayNumber, 0);
        if (dueDays == 0)
        {
            dueDays = 7;
        }

        var outstandingAmount = Math.Max(model.Balance, 0m);
        var gstPercentText = $"{(model.TaxRate * 100):0.##}";
        var orderedPayments = model.Payments.OrderByDescending(x => x.PaymentDate).ToList();

        var bankAccountRows = currency == "USD"
            ? new (string Bank, string Currency, string? AccountName, string? AccountNumber)[]
            {
                ("BML", "USD", bankDetails.BmlUsdAccountName, bankDetails.BmlUsdAccountNumber),
                ("MIB", "USD", bankDetails.MibUsdAccountName, bankDetails.MibUsdAccountNumber)
            }
            : new (string Bank, string Currency, string? AccountName, string? AccountNumber)[]
            {
                ("BML", "MVR", bankDetails.BmlMvrAccountName, bankDetails.BmlMvrAccountNumber),
                ("MIB", "MVR", bankDetails.MibMvrAccountName, bankDetails.MibMvrAccountNumber)
            };

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static (string Fill, string Outline, string Text) StatusColors(PaymentStatus status) => status switch
        {
            PaymentStatus.Paid => ("#DCF6E8", "#95D8B0", "#0F8B57"),
            PaymentStatus.Partial => ("#FFF1D8", "#F2C37E", "#B46A00"),
            _ => ("#E7EDFF", "#AFC1F8", "#4157B2")
        };

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var statusColors = StatusColors(model.PaymentStatus);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text(isTaxApplicable ? "Tax Invoice" : "Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text(isTaxApplicable ? "Tax Invoice" : "Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("INVOICE").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Element(chip =>
                                        chip.Border(1)
                                            .BorderColor(statusColors.Outline)
                                            .Background(statusColors.Fill)
                                            .CornerRadius(12)
                                            .PaddingHorizontal(10)
                                            .PaddingVertical(4)
                                            .Text(model.PaymentStatus.ToString())
                                            .SemiBold()
                                            .FontSize(8.8f)
                                            .FontColor(statusColors.Text));
                                    meta.Item().AlignRight().Text($"Invoice No: {Safe(model.InvoiceNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Issued: {model.DateIssued:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Due: {model.DateDue:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Currency: {currency}");
                                });
                        });

                        header.Item().Text(
                                $"Customer invoice for {Safe(model.CustomerName)} | Delivery Note: {Safe(model.DeliveryNoteNo)} | Due in {dueDays} day(s)")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(billTo =>
                                {
                                    billTo.Spacing(4);
                                    billTo.Item().Text("Bill To").Bold().FontSize(10.4f).FontColor(Ink);
                                    billTo.Item().Text(model.CustomerName).Bold().FontSize(11.4f).FontColor(Ink);
                                    if (isTaxApplicable)
                                    {
                                        billTo.Item().Text($"Customer TIN: {Safe(model.CustomerTinNumber)}").FontColor(Muted);
                                    }
                                    billTo.Item().Text($"Delivery Note: {Safe(model.DeliveryNoteNo)}").FontColor(Muted);
                                    billTo.Item().Text($"Courier / Vessel: {Safe(model.CourierName)}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(92);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("PO Number").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.PoNumber));
                                    if (isTaxApplicable)
                                    {
                                        table.Cell().Element(LabelCell).Text("Tax Rate").SemiBold().FontColor(Muted);
                                        table.Cell().Element(ValueCell).Text($"{gstPercentText}%");
                                    }
                                    table.Cell().Element(LabelCell).Text("Amount Paid").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Money(currency, model.AmountPaid));
                                    table.Cell().Element(LabelCell).Text("Balance").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Money(currency, outstandingAmount)).SemiBold();
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.4f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Subtotal", Money(currency, model.Subtotal)));
                            if (isTaxApplicable)
                            {
                                row.RelativeItem().Element(c => Metric(c, $"GST ({gstPercentText}%)", Money(currency, model.TaxAmount)));
                            }
                            row.RelativeItem().Element(c => Metric(c, "Grand Total", Money(currency, model.GrandTotal)));
                            row.RelativeItem().Element(c => Metric(c, "Received", Money(currency, model.AmountPaid)));
                            row.RelativeItem().Element(c => Metric(c, "Outstanding", Money(currency, outstandingAmount)));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);
                                columns.RelativeColumn(4f);
                                columns.ConstantColumn(74);
                                columns.ConstantColumn(92);
                                columns.ConstantColumn(102);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                            });

                            if (model.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(5)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No line items");
                            }
                            else
                            {
                                for (var i = 0; i < model.Items.Count; i++)
                                {
                                    var item = model.Items[i];
                                    var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                    table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(c => BodyCell(c, background)).Text(item.Description);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.Qty.ToString("N2"));
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(currency, item.Rate));
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(currency, item.Total));
                                }
                            }
                        });

                        if (orderedPayments.Count > 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(payments =>
                                {
                                    payments.Spacing(6);
                                    payments.Item().Text("Payment History").Bold().FontSize(10.4f).FontColor(Ink);
                                    payments.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(96);
                                            columns.ConstantColumn(90);
                                            columns.ConstantColumn(110);
                                            columns.RelativeColumn();
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Method").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Reference / Notes").Bold().FontColor(Ink);
                                        });

                                        for (var i = 0; i < orderedPayments.Count; i++)
                                        {
                                            var payment = orderedPayments[i];
                                            var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                            var paymentCurrency = string.IsNullOrWhiteSpace(payment.Currency)
                                                ? currency
                                                : payment.Currency.Trim().ToUpperInvariant();
                                            var notesText = string.IsNullOrWhiteSpace(payment.Notes)
                                                ? Safe(payment.Reference)
                                                : $"{Safe(payment.Reference)} | {payment.Notes!.Trim()}";

                                            table.Cell().Element(c => BodyCell(c, background)).Text(payment.PaymentDate.ToString("yyyy-MM-dd"));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(payment.Method.ToString());
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(paymentCurrency, payment.Amount));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(notesText);
                                        }
                                    });
                                });
                        }

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(terms =>
                                {
                                    terms.Spacing(4);
                                    terms.Item().Text("Notes & Terms").Bold().FontSize(10.4f).FontColor(Ink);
                                    terms.Item().Text($"Please settle this invoice within {dueDays} day(s) from the issue date.")
                                        .FontColor(Muted);
                                    terms.Item().Text($"Payment status: {model.PaymentStatus}")
                                        .SemiBold()
                                        .FontColor(Ink);
                                    if (!string.IsNullOrWhiteSpace(model.Notes))
                                    {
                                        terms.Item().PaddingTop(2).Text(model.Notes!.Trim()).FontColor(Muted);
                                    }
                                    else
                                    {
                                        terms.Item().PaddingTop(2).Text("No additional notes were recorded for this invoice.")
                                            .FontColor(Muted);
                                    }
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(payment =>
                                {
                                    payment.Spacing(6);
                                    payment.Item().Text("Payment Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    payment.Item().Text($"Authorized by: {Safe(bankDetails.InvoiceOwnerName)}").SemiBold();
                                    payment.Item().Text($"Owner ID: {Safe(bankDetails.InvoiceOwnerIdCard)}").FontColor(Muted);
                                    payment.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(58);
                                            columns.ConstantColumn(56);
                                            columns.RelativeColumn(1.4f);
                                            columns.RelativeColumn(1.25f);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(4);
                                        IContainer BodyCell(IContainer c) => c.Border(1).BorderColor(Border).Padding(4);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("Bank").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Account Name").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Account Number").Bold().FontColor(Ink);
                                        });

                                        foreach (var account in bankAccountRows)
                                        {
                                            table.Cell().Element(BodyCell).AlignCenter().Text(account.Bank);
                                            table.Cell().Element(BodyCell).AlignCenter().Text(account.Currency);
                                            table.Cell().Element(BodyCell).Text(Safe(account.AccountName));
                                            table.Cell().Element(BodyCell).Text(Safe(account.AccountNumber));
                                        }
                                    });
                                });
                        });
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPurchaseOrderPdf(PurchaseOrderDetailDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        var currency = string.Equals((model.Currency ?? string.Empty).Trim(), "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "MVR";
        var gstPercentText = $"{(model.TaxRate * 100):0.##}";
        var leadDays = Math.Max(model.RequiredDate.DayNumber - model.DateIssued.DayNumber, 0);
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Purchase Order").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Purchase Order").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("PURCHASE ORDER").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"PO No: {Safe(model.PurchaseOrderNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Issued: {model.DateIssued:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Required By: {model.RequiredDate:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Currency: {currency}");
                                });
                        });

                        header.Item().Text(
                                $"Order for {Safe(model.SupplierName)} | Lead time {leadDays} day(s) | Courier: {Safe(model.CourierName)}")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(details =>
                                {
                                    details.Spacing(4);
                                    details.Item().Text("Order To").Bold().FontSize(10.4f).FontColor(Ink);
                                    details.Item().Text(model.SupplierName).Bold().FontSize(11.4f).FontColor(Ink);
                                    if (isTaxApplicable)
                                    {
                                        details.Item().Text($"Supplier TIN: {Safe(model.SupplierTinNumber)}").FontColor(Muted);
                                    }
                                    details.Item().Text($"Phone: {Safe(model.SupplierContactNumber)}").FontColor(Muted);
                                    details.Item().Text($"Email: {Safe(model.SupplierEmail)}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(92);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("Courier").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.CourierName));
                                    table.Cell().Element(LabelCell).Text("Required By").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.RequiredDate.ToString("yyyy-MM-dd"));
                                    table.Cell().Element(LabelCell).Text("Lead Time").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text($"{leadDays} day(s)");
                                    if (isTaxApplicable)
                                    {
                                        table.Cell().Element(LabelCell).Text("Tax Rate").SemiBold().FontColor(Muted);
                                        table.Cell().Element(ValueCell).Text($"{gstPercentText}%");
                                    }
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.4f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Subtotal", Money(currency, model.Subtotal)));
                            if (isTaxApplicable)
                            {
                                row.RelativeItem().Element(c => Metric(c, $"GST ({gstPercentText}%)", Money(currency, model.TaxAmount)));
                            }
                            row.RelativeItem().Element(c => Metric(c, "Grand Total", Money(currency, model.GrandTotal)));
                            row.RelativeItem().Element(c => Metric(c, "Items", model.Items.Count.ToString()));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);
                                columns.RelativeColumn(4f);
                                columns.ConstantColumn(74);
                                columns.ConstantColumn(92);
                                columns.ConstantColumn(102);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                            });

                            if (model.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(5)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No purchase order items");
                            }
                            else
                            {
                                for (var i = 0; i < model.Items.Count; i++)
                                {
                                    var item = model.Items[i];
                                    var rowBackground = i % 2 == 0 ? "#FFFFFF" : "#FBFCFF";

                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).Text(item.Description);
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(item.Qty.ToString("N2"));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Rate));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Total));
                                }
                            }
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Notes").Bold().FontSize(10.4f).FontColor(Ink);
                                    notes.Item().Text(
                                            string.IsNullOrWhiteSpace(model.Notes)
                                                ? "No additional purchase order notes were added."
                                                : model.Notes!.Trim())
                                        .FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(info =>
                                {
                                    info.Spacing(4);
                                    info.Item().Text("PO Terms").Bold().FontSize(10.4f).FontColor(Ink);
                                    info.Item().Text("Please confirm quantities, rates, and the required delivery date before fulfillment.")
                                        .FontColor(Muted);
                                    if (!isTaxApplicable)
                                    {
                                        info.Item().Text("Tax is disabled for this business, so this purchase order is saved and exported without tax.")
                                            .FontColor(Muted);
                                    }
                                });
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                        text.Span("Generated on ");
                        text.Span(DateTimeOffset.UtcNow.ToOffset(MaldivesOffset).ToString("yyyy-MM-dd HH:mm"));
                        text.Span(" MVT");
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildQuotationPdf(QuotationDetailDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        var currency = string.Equals((model.Currency ?? string.Empty).Trim(), "USD", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : "MVR";
        var gstPercentText = $"{(model.TaxRate * 100):0.##}";
        var validityDays = Math.Max(model.ValidUntil.DayNumber - model.DateIssued.DayNumber, 0);
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Quotation").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Quotation").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("QUOTATION").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Quotation No: {Safe(model.QuotationNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Issued: {model.DateIssued:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Valid Until: {model.ValidUntil:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Currency: {currency}");
                                });
                        });

                        header.Item().Text(
                                $"Prepared for {Safe(model.CustomerName)} | Valid for {validityDays} day(s) | Courier: {Safe(model.CourierName)}")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(details =>
                                {
                                    details.Spacing(4);
                                    details.Item().Text("Prepared For").Bold().FontSize(10.4f).FontColor(Ink);
                                    details.Item().Text(model.CustomerName).Bold().FontSize(11.4f).FontColor(Ink);
                                    if (isTaxApplicable)
                                    {
                                        details.Item().Text($"Customer TIN: {Safe(model.CustomerTinNumber)}").FontColor(Muted);
                                    }
                                    details.Item().Text($"Phone: {Safe(model.CustomerPhone)}").FontColor(Muted);
                                    details.Item().Text($"Email: {Safe(model.CustomerEmail)}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(92);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("PO Number").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.PoNumber));
                                    table.Cell().Element(LabelCell).Text("Courier").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.CourierName));
                                    table.Cell().Element(LabelCell).Text("Validity").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text($"{validityDays} day(s)");
                                    if (isTaxApplicable)
                                    {
                                        table.Cell().Element(LabelCell).Text("Tax Rate").SemiBold().FontColor(Muted);
                                        table.Cell().Element(ValueCell).Text($"{gstPercentText}%");
                                    }
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.4f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Subtotal", Money(currency, model.Subtotal)));
                            if (isTaxApplicable)
                            {
                                row.RelativeItem().Element(c => Metric(c, $"GST ({gstPercentText}%)", Money(currency, model.TaxAmount)));
                            }
                            row.RelativeItem().Element(c => Metric(c, "Grand Total", Money(currency, model.GrandTotal)));
                            row.RelativeItem().Element(c => Metric(c, "Items", model.Items.Count.ToString()));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);
                                columns.RelativeColumn(4f);
                                columns.ConstantColumn(74);
                                columns.ConstantColumn(92);
                                columns.ConstantColumn(102);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                            });

                            if (model.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(5)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No quotation items");
                            }
                            else
                            {
                                for (var i = 0; i < model.Items.Count; i++)
                                {
                                    var item = model.Items[i];
                                    var rowBackground = i % 2 == 0 ? "#FFFFFF" : "#FBFCFF";

                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).Text(item.Description);
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(item.Qty.ToString("N2"));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Rate));
                                    table.Cell().Element(c => BodyCell(c, rowBackground)).AlignRight().Text(Money(currency, item.Total));
                                }
                            }
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Notes").Bold().FontSize(10.4f).FontColor(Ink);
                                    notes.Item().Text(
                                            string.IsNullOrWhiteSpace(model.Notes)
                                                ? "No additional quotation notes were added."
                                                : model.Notes!.Trim())
                                        .FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(info =>
                                {
                                    info.Spacing(4);
                                    info.Item().Text("Quotation Terms").Bold().FontSize(10.4f).FontColor(Ink);
                                    info.Item().Text("This quotation is valid until the date shown above and may be converted into a delivery note after approval.")
                                        .FontColor(Muted);
                                    if (!isTaxApplicable)
                                    {
                                        info.Item().Text("Tax is disabled for this business, so this quotation is saved and exported without tax.")
                                            .FontColor(Muted);
                                    }
                                });
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                        text.Span("Generated on ");
                        text.Span(DateTimeOffset.UtcNow.ToOffset(MaldivesOffset).ToString("yyyy-MM-dd HH:mm"));
                        text.Span(" MVT");
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPaymentVoucherPdf(PaymentVoucherDetailDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(60).Height(60).Border(1).BorderColor(Border).Background(Colors.White).Padding(4).AlignCenter().AlignMiddle().Element(c => RenderLogo(c, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Payment Voucher").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Payment Voucher").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(220)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .Padding(10)
                                .Column(meta =>
                                {
                                    meta.Spacing(3);
                                    meta.Item().AlignRight().Text("PAYMENT VOUCHER").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Voucher No: {Safe(model.VoucherNumber)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Date: {model.Date:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Status: {model.Status}");
                                });
                        });

                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .Padding(10)
                                .Column(left =>
                                {
                                    left.Spacing(4);
                                    left.Item().Text("Payment Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    left.Item().Text($"Pay To: {Safe(model.PayTo)}");
                                    left.Item().Text($"Details: {Safe(model.Details)}").FontColor(Muted);
                                    left.Item().Text($"Amount: {model.Amount:N2}").Bold();
                                    left.Item().Text($"Amount in Words: {Safe(model.AmountInWords)}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(96);
                                        columns.RelativeColumn();
                                    });

                                    IContainer Label(IContainer c) => c.PaddingVertical(2);
                                    IContainer Value(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(Label).Text("Method").SemiBold().FontColor(Muted);
                                    table.Cell().Element(Value).Text(model.PaymentMethod.ToString());
                                    table.Cell().Element(Label).Text("Bank").SemiBold().FontColor(Muted);
                                    table.Cell().Element(Value).Text(Safe(model.Bank));
                                    table.Cell().Element(Label).Text("Account No").SemiBold().FontColor(Muted);
                                    table.Cell().Element(Value).Text(Safe(model.AccountNumber));
                                    table.Cell().Element(Label).Text("Cheque No").SemiBold().FontColor(Muted);
                                    table.Cell().Element(Value).Text(Safe(model.ChequeNumber));
                                });
                        });

                        column.Item()
                            .Border(1)
                            .BorderColor(Border)
                            .Background(Colors.White)
                            .Padding(10)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(130);
                                    columns.RelativeColumn();
                                });

                                IContainer HeaderCell(IContainer c) => c.BorderBottom(1).BorderColor(Border).PaddingBottom(4);
                                IContainer BodyCell(IContainer c) => c.PaddingVertical(4);

                                table.Cell().Element(HeaderCell).Text("Field").Bold().FontColor(Ink);
                                table.Cell().Element(HeaderCell).Text("Value").Bold().FontColor(Ink);

                                void Row(string label, string value)
                                {
                                    table.Cell().Element(BodyCell).Text(label).SemiBold().FontColor(Muted);
                                    table.Cell().Element(BodyCell).Text(value);
                                }

                                Row("Received By", Safe(model.ReceivedBy));
                                Row("Approved By", Safe(model.ApprovedBy));
                                Row("Linked Supplier Invoice", Safe(model.LinkedReceivedInvoiceNumber));
                                Row("Linked Expense Entry", Safe(model.LinkedExpenseDocumentNumber));
                                Row("Notes", Safe(model.Notes));
                            });

                        column.Item()
                            .Border(1)
                            .BorderColor(Border)
                            .Background(HeaderFill)
                            .Padding(10)
                            .Row(row =>
                            {
                                row.RelativeItem().Text("Authorized Signature").SemiBold().FontColor(Muted);
                                row.RelativeItem().AlignRight().Text("Received Signature").SemiBold().FontColor(Muted);
                            });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generated by myDhathuru on ").FontColor(Muted);
                        text.Span(DateTimeOffset.UtcNow.ToOffset(MaldivesOffset).ToString("yyyy-MM-dd HH:mm")).SemiBold().FontColor(Ink);
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildStatementPdf(AccountStatementDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static string DualTotals(StatementCurrencyTotalsDto totals) => $"MVR {totals.Mvr:N2} | USD {totals.Usd:N2}";
        static string DateLabel(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "-";

        static (string Fill, string Outline, string Text) StatusColors(AccountStatementDto statement)
        {
            var hasPendingBalance = statement.TotalPending.Mvr > 0m || statement.TotalPending.Usd > 0m;
            return hasPendingBalance
                ? ("#FFF1D8", "#F2C37E", "#B46A00")
                : ("#DCF6E8", "#95D8B0", "#0F8B57");
        }

        void Metric(IContainer container, string label, string value, string detail)
        {
            container.Border(1)
                .BorderColor(Border)
                .Background(Colors.White)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.4f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(11).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8.2f).FontColor(Muted);
                });
        }

        var invoiceCount = model.Rows.Count(x => x.Amount > 0m);
        var paymentCount = model.Rows.Count(x => x.Payments > 0m);
        var latestActivity = model.Rows
            .Select(x => x.ReceivedOn ?? x.Date)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasLatestActivity = latestActivity != default;
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var statusColors = StatusColors(model);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.4f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Statement Of Account").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Statement Of Account").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(246)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("STATEMENT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Element(chip =>
                                        chip.Border(1)
                                            .BorderColor(statusColors.Outline)
                                            .Background(statusColors.Fill)
                                            .CornerRadius(12)
                                            .PaddingHorizontal(10)
                                            .PaddingVertical(4)
                                            .Text((model.TotalPending.Mvr > 0m || model.TotalPending.Usd > 0m) ? "Open Balance" : "Settled")
                                            .SemiBold()
                                            .FontSize(8.8f)
                                            .FontColor(statusColors.Text));
                                    meta.Item().AlignRight().Text($"Statement No: {Safe(model.StatementNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Customer: {Safe(model.CustomerName)}");
                                    meta.Item().AlignRight().Text($"Year: {model.Year}");
                                    meta.Item().AlignRight().Text(
                                        hasLatestActivity
                                            ? $"Latest Activity: {latestActivity:yyyy-MM-dd}"
                                            : "Latest Activity: -");
                                });
                        });

                        header.Item()
                            .Text($"Activity summary: {invoiceCount:N0} invoice line(s), {paymentCount:N0} payment line(s), {model.Rows.Count:N0} total statement row(s).")
                            .FontSize(8.5f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(details =>
                                {
                                    details.Spacing(4);
                                    details.Item().Text("Customer Profile").Bold().FontSize(10.4f).FontColor(Ink);
                                    details.Item().Text(model.CustomerName).Bold().FontSize(11.2f);
                                    details.Item().Text($"Statement No: {Safe(model.StatementNo)}").FontColor(Muted);
                                    details.Item().Text($"Reporting Year: {model.Year}").FontColor(Muted);
                                    details.Item().Text($"Generated Rows: {model.Rows.Count:N0}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(profile =>
                                {
                                    profile.Spacing(4);
                                    profile.Item().Text("Statement Profile").Bold().FontSize(10.4f).FontColor(Ink);
                                    profile.Item().Text(
                                            "Balances are tracked independently by currency so MVR and USD activity remain clear and auditable.")
                                        .FontColor(Muted);
                                    profile.Item().Text($"Invoice Rows: {invoiceCount:N0}").FontColor(Muted);
                                    profile.Item().Text($"Payment Rows: {paymentCount:N0}").FontColor(Muted);
                                    profile.Item().Text(
                                            hasLatestActivity
                                                ? $"Last Movement: {latestActivity:yyyy-MM-dd}"
                                                : "Last Movement: -")
                                        .FontColor(Muted);
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => Metric(c, "Opening Balance", DualTotals(model.OpeningBalance), "Balance carried forward before this statement year."));
                            row.RelativeItem().Element(c => Metric(c, "Total Invoiced", DualTotals(model.TotalInvoiced), "Invoice amounts posted during the selected year."));
                            row.RelativeItem().Element(c => Metric(c, "Total Received", DualTotals(model.TotalReceived), "Payments received against statement activity."));
                            row.RelativeItem().Element(c => Metric(c, "Pending Balance", DualTotals(model.TotalPending), "Outstanding balance still open by currency."));
                        });

                        if (model.Rows.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(16)
                                .Padding(18)
                                .AlignCenter()
                                .Text("No statement activity was recorded for the selected year.")
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(32);
                                    columns.ConstantColumn(66);
                                    columns.RelativeColumn(2.7f);
                                    columns.ConstantColumn(96);
                                    columns.ConstantColumn(58);
                                    columns.ConstantColumn(88);
                                    columns.ConstantColumn(88);
                                    columns.ConstantColumn(76);
                                    columns.ConstantColumn(88);
                                });

                                IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                                IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Reference").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignCenter().Text("Currency").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Payments").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Received On").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Balance").Bold().FontColor(Ink);
                                });

                                for (var index = 0; index < model.Rows.Count; index++)
                                {
                                    var row = model.Rows[index];
                                    var background = index % 2 == 0 ? "#FFFFFF" : Panel;

                                    table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text(row.Index.ToString()).FontColor(Muted);
                                    table.Cell().Element(c => BodyCell(c, background)).Text(DateLabel(row.Date)).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).Text(row.Description).SemiBold().FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).Text(Safe(row.Reference)).FontColor(Muted);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text(row.Currency).SemiBold().FontColor(Accent);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(row.Currency, row.Amount)).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(row.Currency, row.Payments)).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).Text(DateLabel(row.ReceivedOn)).FontColor(Muted);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(row.Currency, row.Balance)).SemiBold().FontColor(Ink);
                                }
                            });
                        }

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Statement Notes").Bold().FontSize(10.4f).FontColor(Ink);
                                    notes.Item().Text(
                                            "This statement groups invoices, payments, and opening balances for the selected year. Amount and balance columns are shown in the row currency.")
                                        .FontColor(Muted);
                                    notes.Item().Text(
                                            "Use this export for customer reconciliation, finance review, and year-based account visibility.")
                                        .FontColor(Muted);
                                });

                            row.ConstantItem(270)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(6);
                                    summary.Item().Text("Statement Summary").Bold().FontSize(10.4f).FontColor(Ink);

                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Opening Balance").SemiBold().FontColor(Muted);
                                        item.ConstantItem(142).AlignRight().Text(DualTotals(model.OpeningBalance)).SemiBold();
                                    });
                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Total Invoiced").SemiBold().FontColor(Muted);
                                        item.ConstantItem(142).AlignRight().Text(DualTotals(model.TotalInvoiced)).SemiBold();
                                    });
                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Total Received").SemiBold().FontColor(Muted);
                                        item.ConstantItem(142).AlignRight().Text(DualTotals(model.TotalReceived)).SemiBold();
                                    });
                                    summary.Item().LineHorizontal(1).LineColor(Border);
                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Pending Balance").Bold().FontColor(Ink);
                                        item.ConstantItem(142).AlignRight().Text(DualTotals(model.TotalPending)).Bold().FontSize(10.8f).FontColor(Ink);
                                    });
                                });
                        });
                    });

                    page.Footer().Column(footer =>
                    {
                        footer.Spacing(4);
                        footer.Item().LineHorizontal(1).LineColor(Border);
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru");
                                text.Span(" | ");
                                text.Span($"{generatedAt:yyyy-MM-dd HH:mm} MVT");
                            });

                            row.ConstantItem(78).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalarySlipPdf(SalarySlipDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string SafeDate(DateOnly? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";
        static string Money(decimal value) => $"MVR {Math.Round(value, 2, MidpointRounding.AwayFromZero):N2}";

        var periodLabel = $"{model.PeriodStart:dd/MM/yyyy} to {model.PeriodEnd:dd/MM/yyyy}";
        var periodDays = Math.Max(model.PeriodEnd.DayNumber - model.PeriodStart.DayNumber + 1, 1);
        var expectedDeduction = Round2(model.TotalSalary - model.TotalPayable);
        var totalDeduction = Round2(Math.Abs(model.TotalDeduction - expectedDeduction) > 0.01m ? expectedDeduction : model.TotalDeduction);
        var foodAllowanceCashDeduction = Round2(Math.Max(0m,
            totalDeduction - (model.AbsentDeduction + model.PensionDeduction + model.SalaryAdvanceDeduction)));
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var payoutAccountName = string.IsNullOrWhiteSpace(model.AccountName) ? model.StaffName : model.AccountName;

        var earningRows = new (string Label, decimal Amount)[]
        {
            ("Basic Salary", model.BasicSalary),
            ("Service Allowance", model.ServiceAllowance),
            ("Other Allowance", model.OtherAllowance),
            ("Phone Allowance", model.PhoneAllowance),
            ("Food Allowance", model.FoodAllowance),
            ("Overtime Pay", model.OvertimePay)
        };

        var deductionRows = new (string Label, string Basis, decimal Amount)[]
        {
            ("Absent Deduction", model.AbsentDays > 0 ? $"{model.AbsentDays:N0} x {model.RatePerDay:N2}" : "-", model.AbsentDeduction),
            ("Food Allow. (Cash)", model.FoodAllowanceDays > 0 ? $"{model.FoodAllowanceDays:N0} x {model.FoodAllowanceRate:N2}" : "-", foodAllowanceCashDeduction),
            ("Pension", "-", model.PensionDeduction),
            ("Salary Advance", "-", model.SalaryAdvanceDeduction)
        };

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Salary Slip").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Salary Slip").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(235)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("PAY SLIP").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Slip No: {Safe(model.SlipNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Period: {periodLabel}");
                                    meta.Item().AlignRight().Text($"Staff Code: {Safe(model.StaffCode)}");
                                });
                        });

                        header.Item().Text(
                                $"Payroll period {model.PeriodStart:yyyy-MM-dd} to {model.PeriodEnd:yyyy-MM-dd} | Net payable: {Money(model.TotalPayable)}")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.4f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Net Payable", Money(model.TotalPayable)));
                            row.RelativeItem().Element(c => Metric(c, "Total Salary", Money(model.TotalSalary)));
                            row.RelativeItem().Element(c => Metric(c, "Total Deduction", Money(totalDeduction)));
                            row.RelativeItem().Element(c => Metric(c, "Attendance", $"{model.AttendedDays}/{periodDays} days"));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(112);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("Staff Name").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.StaffName).SemiBold();
                                    table.Cell().Element(LabelCell).Text("Staff Code").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.StaffCode));
                                    table.Cell().Element(LabelCell).Text("ID Number").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.IdNumber));
                                    table.Cell().Element(LabelCell).Text("Phone Number").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.PhoneNumber));
                                    table.Cell().Element(LabelCell).Text("Mail").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.Email));
                                    table.Cell().Element(LabelCell).Text("Hired Date").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(SafeDate(model.HiredDate));
                                    table.Cell().Element(LabelCell).Text("Designation").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.Designation));
                                    table.Cell().Element(LabelCell).Text("Work Site").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(model.WorkSite));
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(118);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("Period Days").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(periodDays.ToString("N0"));
                                    table.Cell().Element(LabelCell).Text("Attended Days").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.AttendedDays.ToString("N0"));
                                    table.Cell().Element(LabelCell).Text("Absent Days").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.AbsentDays.ToString("N0"));
                                    table.Cell().Element(LabelCell).Text("Rate Per Day").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Money(model.RatePerDay));
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(earnings =>
                                {
                                    earnings.Spacing(6);
                                    earnings.Item().Text("Earnings").Bold().FontSize(10.4f).FontColor(Ink);

                                    earnings.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.ConstantColumn(58);
                                            columns.ConstantColumn(96);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                                        });

                                        for (var i = 0; i < earningRows.Length; i++)
                                        {
                                            var item = earningRows[i];
                                            var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.Label);
                                            table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text("MVR");
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.Amount.ToString("N2"));
                                        }

                                        table.Cell().Element(HeaderCell).Text("Total Salary").Bold().FontColor(Ink);
                                        table.Cell().Element(HeaderCell).AlignCenter().Text("MVR").Bold().FontColor(Ink);
                                        table.Cell().Element(HeaderCell).AlignRight().Text(model.TotalSalary.ToString("N2")).Bold().FontColor(Ink);
                                    });
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(deductions =>
                                {
                                    deductions.Spacing(6);
                                    deductions.Item().Text("Deductions").Bold().FontSize(10.4f).FontColor(Ink);

                                    deductions.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(1.8f);
                                            columns.RelativeColumn(1.2f);
                                            columns.ConstantColumn(58);
                                            columns.ConstantColumn(96);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Item").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Basis").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                                        });

                                        for (var i = 0; i < deductionRows.Length; i++)
                                        {
                                            var item = deductionRows[i];
                                            var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.Label);
                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.Basis);
                                            table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text("MVR");
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.Amount.ToString("N2"));
                                        }

                                        table.Cell().ColumnSpan(2).Element(HeaderCell).Text("Total Deduction").Bold().FontColor(Ink);
                                        table.Cell().Element(HeaderCell).AlignCenter().Text("MVR").Bold().FontColor(Ink);
                                        table.Cell().Element(HeaderCell).AlignRight().Text(totalDeduction.ToString("N2")).Bold().FontColor(Ink);
                                    });
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);
                                    summary.Item().Text("Settlement Summary").Bold().FontSize(10.4f).FontColor(Ink);
                                    summary.Item().Text($"Total Salary: {Money(model.TotalSalary)}").SemiBold();
                                    summary.Item().Text($"Total Deduction: {Money(totalDeduction)}").SemiBold();
                                    summary.Item().Text($"Net Payable: {Money(model.TotalPayable)}").Bold().FontSize(11.2f);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(bank =>
                                {
                                    bank.Spacing(4);
                                    bank.Item().Text("Bank Transfer Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    bank.Item().Text($"Bank: {Safe(model.BankName)}");
                                    bank.Item().Text($"Account Name: {Safe(payoutAccountName)}");
                                    bank.Item().Text($"Account Number: {Safe(model.AccountNumber)}");
                                    bank.Item().Text($"Food Allowance: {model.FoodAllowanceDays:N0} day(s) x {Money(model.FoodAllowanceRate)}")
                                        .FontColor(Muted);
                                });
                        });
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("This is a system-generated salary slip.");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }


    public byte[] BuildCustomersPdf(IReadOnlyList<CustomerDto> customers, string companyName, string companyInfo, string? logoUrl, CustomerListQuery query)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        void Metric(IContainer container, string label, string value, string detail, string fillColor)
        {
            container.Border(1)
                .BorderColor(Border)
                .Background(fillColor)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.4f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(11.2f).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8.1f).FontColor(Muted);
                });
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var generatedAt = ToMaldivesTime(DateTimeOffset.UtcNow);
        var searchLabel = string.IsNullOrWhiteSpace(query.Search) ? "All customers" : query.Search.Trim();
        var sortLabel = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? "Name (A-Z)"
            : "Newest first";
        var customersWithEmail = customers.Count(x => !string.IsNullOrWhiteSpace(x.Email));
        var customersWithPhone = customers.Count(x => !string.IsNullOrWhiteSpace(x.Phone));
        var customersWithReferences = customers.Count(x => x.References.Any());
        var totalReferences = customers.Sum(x => x.References.Count);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.3f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Customer Directory").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Customer Directory").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(280)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("CUSTOMER EXPORT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Records: {customers.Count:N0}").SemiBold();
                                    meta.Item().AlignRight().Text($"Search: {searchLabel}").FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Sort: {sortLabel}").FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm} MVT").FontSize(8.6f);
                                });
                        });

                        header.Item()
                            .Text("Customer contact records exported in a cleaner management-ready directory format.")
                            .FontSize(8.5f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => Metric(c, "Total Customers", customers.Count.ToString("N0"), "Customer records included in this export.", "#EEF3FF"));
                            row.RelativeItem().Element(c => Metric(c, "With Email", customersWithEmail.ToString("N0"), "Records carrying an email address.", "#ECFAF6"));
                            row.RelativeItem().Element(c => Metric(c, "With Phone", customersWithPhone.ToString("N0"), "Records carrying a phone number.", "#EFF8FF"));
                            row.RelativeItem().Element(c => Metric(c, "Reference Links", totalReferences.ToString("N0"), $"{customersWithReferences:N0} customer(s) include linked references.", "#F4F1FF"));
                        });

                        if (customers.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .PaddingVertical(34)
                                .AlignCenter()
                                .Text("No customers matched the current export filter.")
                                .SemiBold()
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Spacing(8);
                                    section.Item().Text("Customer List").Bold().FontSize(10.5f).FontColor(Ink);

                                    section.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2.6f);
                                            columns.RelativeColumn(1.3f);
                                            columns.RelativeColumn(1.4f);
                                            columns.RelativeColumn(2.2f);
                                            columns.RelativeColumn(2.1f);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Customer Name").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("TIN").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Phone").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Email").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("References").Bold().FontColor(Ink);
                                        });

                                        for (var index = 0; index < customers.Count; index++)
                                        {
                                            var customer = customers[index];
                                            var background = index % 2 == 0 ? "#FFFFFF" : Panel;
                                            var references = customer.References.Any() ? string.Join(", ", customer.References) : "-";

                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(customer.Name));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(customer.TinNumber));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(customer.Phone));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(customer.Email));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(references));
                                        }

                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text("TOTAL").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(customers.Count.ToString("N0")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(customersWithPhone.ToString("N0")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(customersWithEmail.ToString("N0")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(totalReferences.ToString("N0")).Bold().FontColor(Ink);
                                    });
                                });
                        }
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPayrollPeriodPdf(PayrollPeriodDetailDto model, string companyName, string companyInfo)
    {
        const string Ink = "#263A63";
        const string Muted = "#6D7EA3";
        const string Border = "#D9E3F6";
        const string HeaderFill = "#EEF3FF";
        const string SummaryFill = "#F7FAFF";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string SafeDate(DateOnly? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";
        static string Money(decimal value) => value.ToString("N2");

        var totalPay = model.Entries.Sum(x => x.TotalPay);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A3.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(7f).FontColor(Ink));

                    page.Header().Column(column =>
                    {
                        column.Spacing(3);
                        column.Item().Text(companyName).Bold().FontSize(16);
                        column.Item().Text(companyInfo).FontSize(8).FontColor(Muted);
                        column.Item().PaddingTop(3).Text($"Payroll Detail - {model.Year}-{model.Month:00}").Bold().FontSize(11.5f);
                        column.Item().Text(
                            $"Period: {model.StartDate:yyyy-MM-dd} to {model.EndDate:yyyy-MM-dd} | Days: {model.PeriodDays} | Entries: {model.Entries.Count} | Total Net Payable: MVR {model.TotalNetPayable:N2}")
                            .FontSize(8)
                            .FontColor(Muted);
                    });

                    page.Content().Column(content =>
                    {
                        content.Spacing(8);

                        content.Item().Row(row =>
                        {
                            row.Spacing(6);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(SummaryFill)
                                    .CornerRadius(10)
                                    .PaddingVertical(6)
                                    .PaddingHorizontal(8)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.2f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(9.2f).Bold();
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Entries", model.Entries.Count.ToString("N0")));
                            row.RelativeItem().Element(c => Metric(c, "Period Days", model.PeriodDays.ToString("N0")));
                            row.RelativeItem().Element(c => Metric(c, "Total Pay", $"MVR {totalPay:N2}"));
                            row.RelativeItem().Element(c => Metric(c, "Total Salary", $"MVR {model.TotalNetPayable:N2}"));
                        });

                        content.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.0f);  // staff id
                                columns.RelativeColumn(1.7f);  // staff name
                                columns.RelativeColumn(1.1f);  // designation
                                columns.RelativeColumn(1.0f);  // work site
                                columns.RelativeColumn(0.95f); // basic
                                columns.RelativeColumn(1.0f);  // service allowance
                                columns.RelativeColumn(1.0f);  // other allowance
                                columns.RelativeColumn(1.0f);  // phone allowance
                                columns.RelativeColumn(0.95f); // sub total
                                columns.RelativeColumn(0.7f);  // attended
                                columns.RelativeColumn(0.9f);  // rate / day
                                columns.RelativeColumn(0.8f);  // food days
                                columns.RelativeColumn(0.95f); // absent deduction
                                columns.RelativeColumn(0.8f);  // absent days
                                columns.RelativeColumn(0.95f); // food rate
                                columns.RelativeColumn(1.0f);  // food allowance
                                columns.RelativeColumn(0.85f); // ot pay
                                columns.RelativeColumn(0.9f);  // pension
                                columns.RelativeColumn(1.0f);  // salary advance
                                columns.RelativeColumn(0.95f); // total pay
                                columns.RelativeColumn(1.25f); // account number
                                columns.RelativeColumn(1.0f);  // total salary
                            });

                            IContainer HeaderCell(IContainer c) => c
                                .Border(1)
                                .BorderColor(Border)
                                .Background(HeaderFill)
                                .PaddingVertical(4)
                                .PaddingHorizontal(3)
                                .AlignMiddle();

                            IContainer BodyCell(IContainer c) => c
                                .Border(1)
                                .BorderColor(Border)
                                .PaddingVertical(3)
                                .PaddingHorizontal(3);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Staff ID").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Staff / Mail").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Role / ID No").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Site / Hired").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Basic").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Service").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Other").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Phone").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Sub Total").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Attended").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Rate / Day").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Food Days").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Absent").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Absent Days").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Food A Rate").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Food Allowance").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("OT Pay").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Pension").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Salary Advance").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Total Pay").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Account / Phone").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Total Salary").Bold();
                            });

                            foreach (var entry in model.Entries)
                            {
                                table.Cell().Element(BodyCell).Text(entry.StaffCode);
                                table.Cell().Element(BodyCell).Column(column =>
                                {
                                    column.Spacing(2);
                                    column.Item().Text(entry.StaffName);
                                    column.Item().Text(Safe(entry.Email)).FontSize(6.1f).FontColor(Muted);
                                });
                                table.Cell().Element(BodyCell).Column(column =>
                                {
                                    column.Spacing(2);
                                    column.Item().Text(Safe(entry.Designation));
                                    column.Item().Text(Safe(entry.IdNumber)).FontSize(6.1f).FontColor(Muted);
                                });
                                table.Cell().Element(BodyCell).Column(column =>
                                {
                                    column.Spacing(2);
                                    column.Item().Text(Safe(entry.WorkSite));
                                    column.Item().Text(SafeDate(entry.HiredDate)).FontSize(6.1f).FontColor(Muted);
                                });
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.Basic));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.ServiceAllowance));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.OtherAllowance));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.PhoneAllowance));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.SubTotal));
                                table.Cell().Element(BodyCell).AlignRight().Text(entry.AttendedDays.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.RatePerDay));
                                table.Cell().Element(BodyCell).AlignRight().Text(entry.FoodAllowanceDays.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.AbsentDeduction));
                                table.Cell().Element(BodyCell).AlignRight().Text(entry.AbsentDays.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.FoodAllowanceRate));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.FoodAllowance));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.OvertimePay));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.PensionDeduction));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.SalaryAdvanceDeduction));
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.TotalPay));
                                table.Cell().Element(BodyCell).Column(column =>
                                {
                                    column.Spacing(2);
                                    column.Item().Text(Safe(entry.AccountNumber));
                                    column.Item().Text(Safe(entry.PhoneNumber)).FontSize(6.1f).FontColor(Muted);
                                });
                                table.Cell().Element(BodyCell).AlignRight().Text(Money(entry.NetPayable));
                            }

                            table.Cell().ColumnSpan(19).Element(BodyCell).AlignRight().Text("TOTAL").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(Money(totalPay)).Bold();
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).AlignRight().Text(Money(model.TotalNetPayable)).Bold();
                        });
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesSummaryReportPdf(SalesSummaryReportDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";

        void Metric(IContainer container, string label, string value, string detail, string fillColor)
        {
            container.Border(1)
                .BorderColor(Border)
                .Background(fillColor)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.4f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(11.2f).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8.1f).FontColor(Muted);
                });
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var strongestInvoiceDay = model.Rows
            .OrderByDescending(x => x.InvoiceCount)
            .ThenByDescending(x => x.SalesMvr + x.SalesUsd)
            .FirstOrDefault();
        var strongestMvrDay = model.Rows.OrderByDescending(x => x.SalesMvr).FirstOrDefault();
        var strongestUsdDay = model.Rows.OrderByDescending(x => x.SalesUsd).FirstOrDefault();
        var openExposureDays = model.Rows.Count(x => x.PendingMvr > 0m || x.PendingUsd > 0m);
        var averageInvoicesPerDay = model.Rows.Count == 0 ? 0m : Math.Round((decimal)model.TotalInvoices / model.Rows.Count, 2, MidpointRounding.AwayFromZero);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.3f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Sales Summary Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Sales Summary Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(270)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("SUMMARY REPORT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Preset: {model.Meta.DatePreset}").SemiBold();
                                    meta.Item().AlignRight().Text(
                                        $"Range: {ToMaldivesTime(model.Meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(model.Meta.RangeEndUtc):yyyy-MM-dd HH:mm} MVT")
                                        .FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Customer Scope: {model.Meta.CustomerFilterLabel}").FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Generated: {ToMaldivesTime(model.Meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT").FontSize(8.6f);
                                });
                        });

                        header.Item()
                            .Text("Daily billing, receipts, and exposure trends aligned into one management-ready reporting sheet.")
                            .FontSize(8.5f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(profile =>
                                {
                                    profile.Spacing(4);
                                    profile.Item().Text("Reporting Profile").Bold().FontSize(10.4f).FontColor(Ink);
                                    profile.Item().Text($"Rows in summary: {model.Rows.Count:N0}").FontColor(Muted);
                                    profile.Item().Text($"Distinct customers billed: {model.TotalCustomers:N0}").FontColor(Muted);
                                    profile.Item().Text(
                                            strongestInvoiceDay is null
                                                ? "Peak invoice day: -"
                                                : $"Peak invoice day: {strongestInvoiceDay.Date:yyyy-MM-dd} ({strongestInvoiceDay.InvoiceCount:N0} invoice(s))")
                                        .FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(highlights =>
                                {
                                    highlights.Spacing(4);
                                    highlights.Item().Text("Commercial Highlights").Bold().FontSize(10.4f).FontColor(Ink);
                                    highlights.Item().Text(
                                            strongestMvrDay is null
                                                ? "Highest MVR day: -"
                                                : $"Highest MVR day: {strongestMvrDay.Date:yyyy-MM-dd} ({Money("MVR", strongestMvrDay.SalesMvr)})")
                                        .FontColor(Muted);
                                    highlights.Item().Text(
                                            strongestUsdDay is null
                                                ? "Highest USD day: -"
                                                : $"Highest USD day: {strongestUsdDay.Date:yyyy-MM-dd} ({Money("USD", strongestUsdDay.SalesUsd)})")
                                        .FontColor(Muted);
                                    highlights.Item().Text($"Days carrying open exposure: {openExposureDays:N0}").FontColor(Muted);
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => Metric(c, "Total Invoices", model.TotalInvoices.ToString("N0"), "Invoices issued in the selected range.", "#EEF3FF"));
                            row.RelativeItem().Element(c => Metric(c, "Total Customers", model.TotalCustomers.ToString("N0"), "Distinct billed customers included.", "#F2FBF7"));
                            row.RelativeItem().Element(c => Metric(c, "Sales Mix", $"{Money("MVR", model.TotalSales.Mvr)} | {Money("USD", model.TotalSales.Usd)}", "Gross billed amount by currency.", "#F4F1FF"));
                            row.RelativeItem().Element(c => Metric(c, "Collections", $"{Money("MVR", model.TotalReceived.Mvr)} | {Money("USD", model.TotalReceived.Usd)}", "Cash received inside the reporting range.", "#EEF9FF"));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => Metric(c, "Pending Exposure", $"{Money("MVR", model.TotalPending.Mvr)} | {Money("USD", model.TotalPending.Usd)}", "Open receivables still unsettled.", "#FFF4F7"));
                            if (isTaxApplicable)
                            {
                                row.RelativeItem().Element(c => Metric(c, "Tax Posted", $"{Money("MVR", model.TotalTax.Mvr)} | {Money("USD", model.TotalTax.Usd)}", "Tax recognized across all invoices.", "#FFF8EE"));
                            }
                            row.RelativeItem().Element(c => Metric(c, "Avg Invoices / Day", averageInvoicesPerDay.ToString("N2"), "Average invoice volume per reported day.", "#F9F8FF"));
                            row.RelativeItem().Element(c => Metric(c, "Coverage Days", model.Rows.Count.ToString("N0"), "Daily rows present in the exported range.", "#F6FBFF"));
                            if (!isTaxApplicable)
                            {
                                row.RelativeItem().Element(c => Metric(c, "Exposure Days", openExposureDays.ToString("N0"), "Days that still carried a balance.", "#FFF8EE"));
                            }
                        });

                        if (model.Rows.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .PaddingVertical(34)
                                .AlignCenter()
                                .Text("No sales summary rows were available for the selected range.")
                                .SemiBold()
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Spacing(8);
                                    section.Item().Text("Daily Sales Breakdown").Bold().FontSize(10.5f).FontColor(Ink);

                                    section.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(82);
                                            columns.ConstantColumn(56);
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Invoices").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Sales MVR").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Sales USD").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Received MVR").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Received USD").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Pending MVR").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Pending USD").Bold().FontColor(Ink);
                                        });

                                        for (var index = 0; index < model.Rows.Count; index++)
                                        {
                                            var item = model.Rows.ElementAt(index);
                                            var background = index % 2 == 0 ? "#FFFFFF" : Panel;

                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.Date.ToString("yyyy-MM-dd"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.InvoiceCount.ToString("N0"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.SalesMvr.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.SalesUsd.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.ReceivedMvr.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.ReceivedUsd.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.PendingMvr.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.PendingUsd.ToString("N2"));
                                        }

                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text("TOTAL").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalInvoices.ToString("N0")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalSales.Mvr.ToString("N2")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalSales.Usd.ToString("N2")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalReceived.Mvr.ToString("N2")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalReceived.Usd.ToString("N2")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalPending.Mvr.ToString("N2")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalPending.Usd.ToString("N2")).Bold().FontColor(Ink);
                                    });
                                });
                        }
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesTransactionsReportPdf(SalesTransactionsReportDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static (string Fill, string Outline, string Text) StatusColors(string status) => status.Trim() switch
        {
            "Paid" => ("#DCF6E8", "#95D8B0", "#0F8B57"),
            "Partial" => ("#FFF1D8", "#F2C37E", "#B46A00"),
            _ => ("#FFE7EF", "#F2A8BF", "#B33E63")
        };

        void Metric(IContainer container, string label, string value, string detail, string fillColor)
        {
            container.Border(1)
                .BorderColor(Border)
                .Background(fillColor)
                .CornerRadius(14)
                .Padding(9)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.1f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(10.6f).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(7.8f).FontColor(Muted);
                });
        }

        void StatusChip(IContainer container, string status)
        {
            var colors = StatusColors(status);
            container.AlignCenter().AlignMiddle().PaddingVertical(2).Element(chip =>
                chip.Border(1)
                    .BorderColor(colors.Outline)
                    .Background(colors.Fill)
                    .CornerRadius(10)
                    .PaddingHorizontal(6)
                    .PaddingVertical(3)
                    .Text(status.Trim())
                    .SemiBold()
                    .FontSize(7.4f)
                    .FontColor(colors.Text));
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var paidCount = model.Rows.Count(x => string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase));
        var partialCount = model.Rows.Count(x => string.Equals(x.PaymentStatus, "Partial", StringComparison.OrdinalIgnoreCase));
        var openCount = Math.Max(model.Rows.Count - paidCount - partialCount, 0);
        var cashCount = model.Rows.Count(x => !string.Equals(x.PaymentMethod, "-", StringComparison.OrdinalIgnoreCase));
        var latestReceipt = model.Rows
            .Where(x => x.ReceivedOn.HasValue)
            .Select(x => x.ReceivedOn!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasLatestReceipt = latestReceipt != default;

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A3.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(8.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Sales Transactions Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Sales Transactions Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(285)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("TRANSACTION REPORT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Preset: {model.Meta.DatePreset}").SemiBold();
                                    meta.Item().AlignRight().Text(
                                        $"Range: {ToMaldivesTime(model.Meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(model.Meta.RangeEndUtc):yyyy-MM-dd HH:mm} MVT")
                                        .FontSize(8.4f);
                                    meta.Item().AlignRight().Text($"Customer Scope: {model.Meta.CustomerFilterLabel}").FontSize(8.4f);
                                    meta.Item().AlignRight().Text($"Generated: {ToMaldivesTime(model.Meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT").FontSize(8.4f);
                                });
                        });

                        header.Item()
                            .Text("Invoice-level activity with payment state, receipt timing, and balance exposure across every exported transaction.")
                            .FontSize(8.2f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => Metric(c, "Transactions", model.TotalTransactions.ToString("N0"), "Invoice rows included in this export.", "#EEF3FF"));
                            row.RelativeItem().Element(c => Metric(c, "Gross Sales", $"{Money("MVR", model.TotalSales.Mvr)} | {Money("USD", model.TotalSales.Usd)}", "Total invoiced amount by currency.", "#ECFAF6"));
                            row.RelativeItem().Element(c => Metric(c, "Collections", $"{Money("MVR", model.TotalReceived.Mvr)} | {Money("USD", model.TotalReceived.Usd)}", "Payments recorded against these invoices.", "#EEF9FF"));
                            row.RelativeItem().Element(c => Metric(c, "Open Balance", $"{Money("MVR", model.TotalPending.Mvr)} | {Money("USD", model.TotalPending.Usd)}", "Outstanding value still unpaid.", "#FFF4F7"));
                            row.RelativeItem().Element(c => Metric(c, "Latest Receipt", hasLatestReceipt ? latestReceipt.ToString("yyyy-MM-dd") : "-", "Most recent payment date present in the export.", "#FFF8EE"));
                            row.RelativeItem().Element(c => Metric(c, "Active Collection Rows", cashCount.ToString("N0"), "Rows carrying a payment method entry.", "#F7F5FF"));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);
                                    summary.Item().Text("Payment Status Mix").Bold().FontSize(10.1f).FontColor(Ink);
                                    summary.Item().Text($"Paid rows: {paidCount:N0}").FontColor(Muted);
                                    summary.Item().Text($"Partial rows: {partialCount:N0}").FontColor(Muted);
                                    summary.Item().Text($"Open rows: {openCount:N0}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);
                                    summary.Item().Text("Scope and Filters").Bold().FontSize(10.1f).FontColor(Ink);
                                    summary.Item().Text($"Customer filter: {model.Meta.CustomerFilterLabel}").FontColor(Muted);
                                    summary.Item().Text($"Date preset: {model.Meta.DatePreset}").FontColor(Muted);
                                    summary.Item().Text($"Receipt-bearing rows: {model.Rows.Count(x => x.ReceivedOn.HasValue):N0}").FontColor(Muted);
                                });
                        });

                        if (model.Rows.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .PaddingVertical(34)
                                .AlignCenter()
                                .Text("No sales transactions matched the selected report criteria.")
                                .SemiBold()
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Spacing(8);
                                    section.Item().Text("Transaction Register").Bold().FontSize(10.3f).FontColor(Ink);

                                    section.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(96);
                                            columns.ConstantColumn(72);
                                            columns.RelativeColumn(1.2f);
                                            columns.RelativeColumn(1.05f);
                                            columns.RelativeColumn(1.8f);
                                            columns.ConstantColumn(48);
                                            columns.ConstantColumn(76);
                                            columns.ConstantColumn(72);
                                            columns.ConstantColumn(76);
                                            columns.ConstantColumn(72);
                                            columns.ConstantColumn(76);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(4);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(4);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Invoice No").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Issued").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Customer").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Vessel").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("Status").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Method").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Received").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Balance").Bold().FontColor(Ink);
                                        });

                                        for (var index = 0; index < model.Rows.Count; index++)
                                        {
                                            var item = model.Rows.ElementAt(index);
                                            var background = index % 2 == 0 ? "#FFFFFF" : Panel;

                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.InvoiceNo));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.DateIssued.ToString("yyyy-MM-dd"));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.Customer));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.Vessel));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.Description));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text(Safe(item.Currency));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.Amount.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).Element(c => StatusChip(c, item.PaymentStatus));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.PaymentMethod));
                                            table.Cell().Element(c => BodyCell(c, background)).Text(item.ReceivedOn?.ToString("yyyy-MM-dd") ?? "-");
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.Balance.ToString("N2"));
                                        }

                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text("TOTAL").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text($"{model.TotalSales.Mvr:N2} / {model.TotalSales.Usd:N2}").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text(string.Empty);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text($"{model.TotalPending.Mvr:N2} / {model.TotalPending.Usd:N2}").Bold().FontColor(Ink);
                                    });
                                });
                        }
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesByVesselReportPdf(SalesByVesselReportDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        void Metric(IContainer container, string label, string value, string detail, string fillColor)
        {
            container.Border(1)
                .BorderColor(Border)
                .Background(fillColor)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.4f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(11.2f).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8.1f).FontColor(Muted);
                });
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var vesselCount = model.Rows.Select(x => x.Vessel).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var leadMvrVessel = model.Rows
            .Where(x => string.Equals(x.Currency, "MVR", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TotalSales)
            .FirstOrDefault();
        var leadUsdVessel = model.Rows
            .Where(x => string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.TotalSales)
            .FirstOrDefault();
        var topShare = model.Rows.OrderByDescending(x => x.PercentageOfCurrencySales).FirstOrDefault();

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.3f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Sales By Vessel Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Sales By Vessel Report").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(270)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("VESSEL REPORT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Preset: {model.Meta.DatePreset}").SemiBold();
                                    meta.Item().AlignRight().Text(
                                        $"Range: {ToMaldivesTime(model.Meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(model.Meta.RangeEndUtc):yyyy-MM-dd HH:mm} MVT")
                                        .FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Customer Scope: {model.Meta.CustomerFilterLabel}").FontSize(8.6f);
                                    meta.Item().AlignRight().Text($"Generated: {ToMaldivesTime(model.Meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT").FontSize(8.6f);
                                });
                        });

                        header.Item()
                            .Text("Vessel contribution, collections, and outstanding balances grouped by currency for operator-level decision making.")
                            .FontSize(8.5f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => Metric(c, "Transactions", model.TotalTransactions.ToString("N0"), "Total invoice records behind this vessel mix.", "#EEF3FF"));
                            row.RelativeItem().Element(c => Metric(c, "Active Vessels", vesselCount.ToString("N0"), "Distinct vessels appearing in the export.", "#ECFAF6"));
                            row.RelativeItem().Element(c => Metric(c, "Sales Mix", $"{Money("MVR", model.TotalSales.Mvr)} | {Money("USD", model.TotalSales.Usd)}", "Total sales aggregated by currency.", "#EFF8FF"));
                            row.RelativeItem().Element(c => Metric(c, "Pending Mix", $"{Money("MVR", model.TotalPending.Mvr)} | {Money("USD", model.TotalPending.Usd)}", "Outstanding balance by vessel currency mix.", "#FFF4F7"));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);
                                    summary.Item().Text("Lead Vessel Positions").Bold().FontSize(10.2f).FontColor(Ink);
                                    summary.Item().Text(
                                            leadMvrVessel is null
                                                ? "Top MVR vessel: -"
                                                : $"Top MVR vessel: {Safe(leadMvrVessel.Vessel)} ({Money("MVR", leadMvrVessel.TotalSales)})")
                                        .FontColor(Muted);
                                    summary.Item().Text(
                                            leadUsdVessel is null
                                                ? "Top USD vessel: -"
                                                : $"Top USD vessel: {Safe(leadUsdVessel.Vessel)} ({Money("USD", leadUsdVessel.TotalSales)})")
                                        .FontColor(Muted);
                                    summary.Item().Text(
                                            topShare is null
                                                ? "Highest currency share: -"
                                                : $"Highest currency share: {Safe(topShare.Vessel)} ({topShare.PercentageOfCurrencySales:N2}%)")
                                        .FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(summary =>
                                {
                                    summary.Spacing(4);
                                    summary.Item().Text("Collection View").Bold().FontSize(10.2f).FontColor(Ink);
                                    summary.Item().Text($"Received MVR: {Money("MVR", model.TotalReceived.Mvr)}").FontColor(Muted);
                                    summary.Item().Text($"Received USD: {Money("USD", model.TotalReceived.Usd)}").FontColor(Muted);
                                    summary.Item().Text($"Rows with open balance: {model.Rows.Count(x => x.PendingAmount > 0m):N0}").FontColor(Muted);
                                });
                        });

                        if (model.Rows.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .PaddingVertical(34)
                                .AlignCenter()
                                .Text("No vessel sales records were available for the selected range.")
                                .SemiBold()
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(section =>
                                {
                                    section.Spacing(8);
                                    section.Item().Text("Vessel Contribution Table").Bold().FontSize(10.5f).FontColor(Ink);

                                    section.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(1.9f);
                                            columns.ConstantColumn(56);
                                            columns.ConstantColumn(74);
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                            columns.ConstantColumn(84);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Vessel").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Transactions").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Total Sales").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Total Received").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("Pending").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).AlignRight().Text("% of CCY").Bold().FontColor(Ink);
                                        });

                                        for (var index = 0; index < model.Rows.Count; index++)
                                        {
                                            var item = model.Rows.ElementAt(index);
                                            var background = index % 2 == 0 ? "#FFFFFF" : Panel;

                                            table.Cell().Element(c => BodyCell(c, background)).Text(Safe(item.Vessel));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text(Safe(item.Currency));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.TransactionCount.ToString("N0"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.TotalSales.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.TotalReceived.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.PendingAmount.ToString("N2"));
                                            table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text($"{item.PercentageOfCurrencySales:N2}%");
                                        }

                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text("TOTAL").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).Text("-").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text(model.TotalTransactions.ToString("N0")).Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text($"{model.TotalSales.Mvr:N2} / {model.TotalSales.Usd:N2}").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text($"{model.TotalReceived.Mvr:N2} / {model.TotalReceived.Usd:N2}").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text($"{model.TotalPending.Mvr:N2} / {model.TotalPending.Usd:N2}").Bold().FontColor(Ink);
                                        table.Cell().Element(c => BodyCell(c, HeaderFill)).AlignRight().Text("-").Bold().FontColor(Ink);
                                    });
                                });
                        }
                    });

                    page.Footer().Row(footer =>
                    {
                        footer.RelativeItem().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.Span("Generated by myDhathuru");
                            text.Span(" | ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                        });

                        footer.ConstantItem(54).AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPortalAdminInvoicePdf(PortalAdminBillingInvoiceDetailDto model, PortalAdminBillingSettingsDto settings)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(string currencyCode, decimal amount) => $"{currencyCode} {amount:N2}";
        static (string Fill, string Outline, string Text) StatusColors(AdminInvoiceStatus status) => status switch
        {
            AdminInvoiceStatus.Emailed => ("#DCF6E8", "#95D8B0", "#0F8B57"),
            AdminInvoiceStatus.Issued => ("#FFF1D8", "#F2C37E", "#B46A00"),
            AdminInvoiceStatus.EmailFailed => ("#FFE0E7", "#F2A4B6", "#B33E63"),
            AdminInvoiceStatus.Cancelled => ("#ECEFF6", "#C6D0E6", "#60708F"),
            _ => ("#E7EDFF", "#AFC1F8", "#4157B2")
        };

        var monthLabel = model.BillingMonth.ToString("MMMM yyyy");
        var currency = string.IsNullOrWhiteSpace(model.Currency) ? "MVR" : model.Currency.Trim().ToUpperInvariant();
        var logoAsset = ResolvePortalAdminLogo(settings.LogoUrl);
        var statusColors = StatusColors(model.Status);
        var latestEmailLog = model.EmailLogs.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var orderedLineItems = model.LineItems.OrderBy(x => x.SortOrder).ThenBy(x => x.Description, StringComparer.OrdinalIgnoreCase).ToList();
        var orderedEmailLogs = model.EmailLogs.OrderByDescending(x => x.AttemptedAt).ToList();

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(68)
                                            .Height(68)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text("myDhathuru Platform").Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Platform Billing Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
                                            if (!string.IsNullOrWhiteSpace(settings.EmailFromName))
                                            {
                                                text.Item().Text(settings.EmailFromName!).FontSize(8.6f).FontColor(Muted);
                                            }
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text("myDhathuru Platform").Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Platform Billing Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
                                        if (!string.IsNullOrWhiteSpace(settings.EmailFromName))
                                        {
                                            text.Item().Text(settings.EmailFromName!).FontSize(8.6f).FontColor(Muted);
                                        }
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(right =>
                                {
                                    right.Spacing(4);
                                    right.Item().AlignRight().Text("INVOICE").Bold().FontSize(16).FontColor(Ink);
                                    right.Item().AlignRight().Element(chip =>
                                        chip.Border(1)
                                            .BorderColor(statusColors.Outline)
                                            .Background(statusColors.Fill)
                                            .CornerRadius(12)
                                            .PaddingHorizontal(10)
                                            .PaddingVertical(4)
                                            .Text(model.Status.ToString())
                                            .SemiBold()
                                            .FontSize(8.8f)
                                            .FontColor(statusColors.Text));
                                    right.Item().AlignRight().Text($"Invoice No: {Safe(model.InvoiceNumber)}").SemiBold();
                                    right.Item().AlignRight().Text($"Invoice Date: {model.InvoiceDate:yyyy-MM-dd}");
                                    right.Item().AlignRight().Text($"Billing Month: {monthLabel}");
                                    right.Item().AlignRight().Text($"Due Date: {model.DueDate:yyyy-MM-dd}");
                                });
                        });

                        header.Item().Text(
                                $"Platform billing for {Safe(model.CompanyName)} | Staff: {model.StaffCount:N0} | Vessels: {model.VesselCount:N0}")
                            .FontSize(8.6f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(billTo =>
                                {
                                    billTo.Spacing(4);
                                    billTo.Item().Text("Bill To").Bold().FontSize(10.4f).FontColor(Ink);
                                    billTo.Item().Text(model.CompanyName).Bold().FontSize(11.4f).FontColor(Ink);
                                    billTo.Item().Text($"Email: {Safe(model.CompanyEmail)}").FontColor(Muted);
                                    billTo.Item().Text($"Phone: {Safe(model.CompanyPhone)}").FontColor(Muted);
                                    billTo.Item().Text($"TIN: {Safe(model.CompanyTinNumber)}").FontColor(Muted);
                                    billTo.Item().Text($"Registration: {Safe(model.CompanyRegistrationNumber)}").FontColor(Muted);
                                    billTo.Item().Text($"Primary Admin: {Safe(model.CompanyAdminName)} ({Safe(model.CompanyAdminEmail)})")
                                        .FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.ConstantColumn(104);
                                        columns.RelativeColumn();
                                    });

                                    IContainer LabelCell(IContainer c) => c.PaddingVertical(2);
                                    IContainer ValueCell(IContainer c) => c.PaddingVertical(2);

                                    table.Cell().Element(LabelCell).Text("Invoice Type").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(model.IsCustom ? "Custom billing run" : "Standard billing cycle");
                                    table.Cell().Element(LabelCell).Text("Created").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(ToMaldivesTime(model.CreatedAt).ToString("yyyy-MM-dd HH:mm") + " MVT");
                                    table.Cell().Element(LabelCell).Text("Email Status").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(latestEmailLog?.Status.ToString() ?? "Not sent");
                                    table.Cell().Element(LabelCell).Text("Reply To").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text(Safe(settings.ReplyToEmail));
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            void Metric(IContainer container, string label, string value)
                            {
                                container
                                    .Border(1)
                                    .BorderColor(Border)
                                    .Background(Panel)
                                    .CornerRadius(12)
                                    .PaddingVertical(7)
                                    .PaddingHorizontal(9)
                                    .Column(metric =>
                                    {
                                        metric.Spacing(2);
                                        metric.Item().Text(label).FontSize(7.4f).SemiBold().FontColor(Muted);
                                        metric.Item().Text(value).FontSize(10.4f).Bold().FontColor(Ink);
                                    });
                            }

                            row.RelativeItem().Element(c => Metric(c, "Software Fee", Money(currency, model.BaseSoftwareFee)));
                            row.RelativeItem().Element(c => Metric(c, $"Vessel Fees ({model.VesselCount:N0})", Money(currency, model.VesselAmount)));
                            row.RelativeItem().Element(c => Metric(c, $"Staff Fees ({model.StaffCount:N0})", Money(currency, model.StaffAmount)));
                            row.RelativeItem().Element(c => Metric(c, "Total Due", Money(currency, model.Total)));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(36);
                                columns.RelativeColumn(4);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(90);
                                columns.ConstantColumn(90);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(6);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(6);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).Text("Description").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Quantity").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold().FontColor(Ink);
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Ink);
                            });

                            if (orderedLineItems.Count == 0)
                            {
                                table.Cell().ColumnSpan(5)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No billing line items");
                            }
                            else
                            {
                                for (var i = 0; i < orderedLineItems.Count; i++)
                                {
                                    var line = orderedLineItems[i];
                                    var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                    table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(c => BodyCell(c, background)).Text(line.Description);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(line.Quantity.ToString("N2"));
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(currency, line.Rate));
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Money(currency, line.Amount));
                                }
                            }
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(payment =>
                                {
                                    payment.Spacing(4);
                                    payment.Item().Text("Payment Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    payment.Item().Text($"Account Name: {Safe(settings.AccountName)}").SemiBold();
                                    payment.Item().Text($"Account Number: {Safe(settings.AccountNumber)}").FontColor(Muted);
                                    payment.Item().Text($"Bank: {Safe(settings.BankName)} | Branch: {Safe(settings.Branch)}")
                                        .FontColor(Muted);
                                    payment.Item().Text(Safe(settings.PaymentInstructions)).FontColor(Muted);
                                });

                            row.ConstantItem(220)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(totals =>
                                {
                                    totals.Spacing(5);
                                    totals.Item().Text("Invoice Summary").Bold().FontSize(10.4f).FontColor(Ink);
                                    totals.Item().Row(summary =>
                                    {
                                        summary.RelativeItem().Text("Subtotal").SemiBold().FontColor(Muted);
                                        summary.ConstantItem(100).AlignRight().Text(Money(currency, model.Subtotal));
                                    });
                                    totals.Item().Row(summary =>
                                    {
                                        summary.RelativeItem().Text("Total").Bold().FontColor(Ink);
                                        summary.ConstantItem(100).AlignRight().Text(Money(currency, model.Total)).Bold().FontSize(11);
                                    });
                                });
                        });

                        if (!string.IsNullOrWhiteSpace(model.Notes) || !string.IsNullOrWhiteSpace(settings.InvoiceTerms))
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Notes & Terms").Bold().FontSize(10.4f).FontColor(Ink);
                                    if (!string.IsNullOrWhiteSpace(model.Notes))
                                    {
                                        notes.Item().Text(model.Notes!.Trim()).FontColor(Muted);
                                    }

                                    if (!string.IsNullOrWhiteSpace(settings.InvoiceTerms))
                                    {
                                        notes.Item().Text(settings.InvoiceTerms!.Trim()).FontColor(Muted);
                                    }
                                });
                        }

                        if (orderedEmailLogs.Count > 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(logs =>
                                {
                                    logs.Spacing(6);
                                    logs.Item().Text("Email Activity").Bold().FontSize(10.4f).FontColor(Ink);
                                    logs.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.ConstantColumn(110);
                                            columns.ConstantColumn(92);
                                            columns.RelativeColumn(1.4f);
                                            columns.RelativeColumn(1.8f);
                                        });

                                        IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                        IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                        table.Header(header =>
                                        {
                                            header.Cell().Element(HeaderCell).Text("Attempted").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Status").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Recipient").Bold().FontColor(Ink);
                                            header.Cell().Element(HeaderCell).Text("Subject / Error").Bold().FontColor(Ink);
                                        });

                                        for (var i = 0; i < orderedEmailLogs.Count; i++)
                                        {
                                            var log = orderedEmailLogs[i];
                                            var background = i % 2 == 0 ? "#FFFFFF" : Panel;
                                            var subjectText = string.IsNullOrWhiteSpace(log.ErrorMessage)
                                                ? log.Subject
                                                : $"{log.Subject} | {log.ErrorMessage!.Trim()}";
                                            var recipientText = string.IsNullOrWhiteSpace(log.CcEmail)
                                                ? log.ToEmail
                                                : $"{log.ToEmail} | CC: {log.CcEmail!.Trim()}";

                                            table.Cell().Element(c => BodyCell(c, background)).Text(ToMaldivesTime(log.AttemptedAt).ToString("yyyy-MM-dd HH:mm") + " MVT");
                                            table.Cell().Element(c => BodyCell(c, background)).Text(log.Status.ToString());
                                            table.Cell().Element(c => BodyCell(c, background)).Text(recipientText);
                                            table.Cell().Element(c => BodyCell(c, background)).Text(subjectText);
                                        }
                                    });
                                });
                        }
                    });

                    page.Footer().Column(column =>
                    {
                        column.Spacing(2);
                        if (!string.IsNullOrWhiteSpace(settings.InvoiceFooterNote))
                        {
                            column.Item().AlignCenter().Text(settings.InvoiceFooterNote).FontSize(8.3f).FontColor(Muted);
                        }

                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru Portal Admin");
                                text.Span(" | ");
                                text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                            });

                            row.ConstantItem(54).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildExpenseLedgerPdf(IReadOnlyList<ExpenseLedgerRowDto> rows, ExpenseSummaryDto summary, string companyName, string companyInfo, string? logoUrl, ExpenseLedgerQuery query)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string DualCurrency(IReadOnlyList<ExpenseLedgerRowDto> source, Func<ExpenseLedgerRowDto, decimal> selector)
        {
            var mvr = source
                .Where(x => !string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                .Sum(selector);
            var usd = source
                .Where(x => string.Equals(x.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                .Sum(selector);
            return $"MVR {mvr:N2} | USD {usd:N2}";
        }

        static string FilterLabel(ExpenseLedgerQuery value)
        {
            var parts = new List<string>();
            if (value.DateFrom.HasValue || value.DateTo.HasValue)
            {
                parts.Add($"Range: {(value.DateFrom?.ToString("yyyy-MM-dd") ?? "Start")} to {(value.DateTo?.ToString("yyyy-MM-dd") ?? "Today")}");
            }

            if (value.SourceType.HasValue)
            {
                parts.Add($"Source: {value.SourceType.Value}");
            }

            if (value.PendingOnly)
            {
                parts.Add("Pending only");
            }

            if (!string.IsNullOrWhiteSpace(value.Search))
            {
                parts.Add($"Search: {value.Search.Trim()}");
            }

            return parts.Count == 0 ? "All live expense ledger rows" : string.Join(" | ", parts);
        }

        void Metric(IContainer container, string label, string value, string detail)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background(Colors.White)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).FontSize(8.4f).SemiBold().FontColor(Muted);
                    metric.Item().Text(value).FontSize(11).Bold().FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8.2f).FontColor(Muted);
                });
        }

        var generatedAt = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var filterLabel = FilterLabel(query);
        var sourceBreakdown = rows
            .GroupBy(x => x.SourceType)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}: {group.Count():N0}")
            .ToList();
        var topCategories = summary.ByCategory.Take(5).ToList();
        var monthlyMix = summary.ByMonth.TakeLast(6).ToList();

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9.2f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(64)
                                            .Height(64)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Expense Ledger").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Expense Ledger").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.5f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(270)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("LEDGER EXPORT").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"{rows.Count:N0} row(s)").SemiBold();
                                    meta.Item().AlignRight().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm} MVT");
                                    meta.Item().AlignRight().Text(filterLabel).FontSize(8.2f).FontColor(Muted);
                                });
                        });

                        header.Item().Text(
                                sourceBreakdown.Count == 0
                                    ? "Source coverage: no expense sources matched the selected export filter."
                                    : $"Source coverage: {string.Join(" | ", sourceBreakdown)}")
                            .FontSize(8.4f)
                            .FontColor(Muted);
                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(scope =>
                                {
                                    scope.Spacing(4);
                                    scope.Item().Text("Ledger Scope").Bold().FontSize(10.4f).FontColor(Ink);
                                    scope.Item().Text("This export combines received invoices, rent, payroll, and manual expense entries from live tenant data.")
                                        .FontColor(Muted);
                                    scope.Item().Text($"Filter set: {filterLabel}").FontColor(Muted);
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(profile =>
                                {
                                    profile.Spacing(4);
                                    profile.Item().Text("Coverage Snapshot").Bold().FontSize(10.4f).FontColor(Ink);
                                    profile.Item().Text($"Categories represented: {summary.ByCategory.Count:N0}").FontColor(Muted);
                                    profile.Item().Text($"Months represented: {summary.ByMonth.Count:N0}").FontColor(Muted);
                                    profile.Item().Text($"Rows with pending balance: {rows.Count(x => x.PendingAmount > 0m):N0}").FontColor(Muted);
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => Metric(c, "Total Net", DualCurrency(rows, x => x.NetAmount), "Net expense value by currency."));
                            row.RelativeItem().Element(c => Metric(c, "Total Tax", DualCurrency(rows, x => x.TaxAmount), "Tax recognized on purchase-side activity."));
                            row.RelativeItem().Element(c => Metric(c, "Total Gross", DualCurrency(rows, x => x.GrossAmount), "Gross ledger amount across live rows."));
                            row.RelativeItem().Element(c => Metric(c, "Pending", DualCurrency(rows, x => x.PendingAmount), "Outstanding unpaid expense balance."));
                        });

                        if (rows.Count == 0)
                        {
                            column.Item()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(16)
                                .Padding(18)
                                .AlignCenter()
                                .Text("No expense ledger rows matched the selected export filter.")
                                .FontColor(Muted);
                        }
                        else
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(62);
                                    columns.RelativeColumn(2.2f);
                                    columns.ConstantColumn(74);
                                    columns.ConstantColumn(94);
                                    columns.RelativeColumn(1.6f);
                                    columns.ConstantColumn(52);
                                    columns.ConstantColumn(78);
                                    columns.ConstantColumn(70);
                                    columns.ConstantColumn(82);
                                    columns.ConstantColumn(82);
                                });

                                IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                                IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Date").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Document").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Source").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Category").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).Text("Payee").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignCenter().Text("Currency").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Net").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Tax").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Gross").Bold().FontColor(Ink);
                                    header.Cell().Element(HeaderCell).AlignRight().Text("Pending").Bold().FontColor(Ink);
                                });

                                for (var index = 0; index < rows.Count; index++)
                                {
                                    var item = rows[index];
                                    var background = index % 2 == 0 ? "#FFFFFF" : Panel;

                                    table.Cell().Element(c => BodyCell(c, background)).Text(item.TransactionDate.ToString("yyyy-MM-dd")).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).Column(doc =>
                                    {
                                        doc.Spacing(2);
                                        doc.Item().Text(item.DocumentNumber).SemiBold().FontColor(Ink);
                                        if (!string.IsNullOrWhiteSpace(item.Description))
                                        {
                                            doc.Item().Text(item.Description!.Trim()).FontSize(7.6f).FontColor(Muted);
                                        }
                                    });
                                    table.Cell().Element(c => BodyCell(c, background)).Text(item.SourceType).FontColor(Muted);
                                    table.Cell().Element(c => BodyCell(c, background)).Text(item.ExpenseCategoryName).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).Column(payee =>
                                    {
                                        payee.Spacing(2);
                                        payee.Item().Text(item.PayeeName).SemiBold().FontColor(Ink);
                                        if (!string.IsNullOrWhiteSpace(item.SupplierName) && !string.Equals(item.SupplierName, item.PayeeName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            payee.Item().Text(item.SupplierName!.Trim()).FontSize(7.6f).FontColor(Muted);
                                        }
                                    });
                                    table.Cell().Element(c => BodyCell(c, background)).AlignCenter().Text(item.Currency).SemiBold().FontColor(Accent);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.NetAmount.ToString("N2")).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.TaxAmount.ToString("N2")).FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.GrossAmount.ToString("N2")).SemiBold().FontColor(Ink);
                                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(item.PendingAmount.ToString("N2")).SemiBold().FontColor(item.PendingAmount > 0 ? "#B46A00" : Ink);
                                }
                            });
                        }

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(category =>
                                {
                                    category.Spacing(6);
                                    category.Item().Text("Top Categories").Bold().FontSize(10.4f).FontColor(Ink);

                                    if (topCategories.Count == 0)
                                    {
                                        category.Item().Text("No category totals were available for the selected export scope.").FontColor(Muted);
                                    }
                                    else
                                    {
                                        foreach (var item in topCategories)
                                        {
                                            category.Item().Row(line =>
                                            {
                                                line.RelativeItem().Text(item.Label).SemiBold().FontColor(Ink);
                                                line.ConstantItem(100).AlignRight().Text(item.GrossAmount.ToString("N2")).Bold().FontColor(Ink);
                                            });
                                        }
                                    }
                                });

                            row.RelativeItem()
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(month =>
                                {
                                    month.Spacing(6);
                                    month.Item().Text("Monthly Gross Mix").Bold().FontSize(10.4f).FontColor(Ink);

                                    if (monthlyMix.Count == 0)
                                    {
                                        month.Item().Text("No monthly totals were available for the selected export scope.").FontColor(Muted);
                                    }
                                    else
                                    {
                                        foreach (var item in monthlyMix)
                                        {
                                            month.Item().Row(line =>
                                            {
                                                line.RelativeItem().Text(item.Label).SemiBold().FontColor(Ink);
                                                line.ConstantItem(100).AlignRight().Text(item.GrossAmount.ToString("N2")).Bold().FontColor(Ink);
                                            });
                                        }
                                    }
                                });
                        });
                    });

                    page.Footer().Column(footer =>
                    {
                        footer.Spacing(4);
                        footer.Item().LineHorizontal(1).LineColor(Border);
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru");
                                text.Span(" | ");
                                text.Span($"{generatedAt:yyyy-MM-dd HH:mm} MVT");
                            });

                            row.ConstantItem(78).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildStaffConductFormPdf(StaffConductDetailDto model, string companyName, string companyInfo, string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string Accent = "#6F7FF5";
        const string WarningFill = "#FFF6E7";
        const string DisciplineFill = "#FCECF2";
        const string GoodFill = "#EAF8F1";
        const string OpenFill = "#EEF2FF";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        static string LabelForType(StaffConductFormType value)
            => value == StaffConductFormType.Warning ? "Warning Form" : "Disciplinary Form";

        static string DateValue(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "-";

        static string DetailBackground(StaffConductFormType value)
            => value == StaffConductFormType.Warning ? WarningFill : DisciplineFill;

        static string StatusBackground(StaffConductStatus value) => value switch
        {
            StaffConductStatus.Resolved => GoodFill,
            StaffConductStatus.Acknowledged => "#FFF4DE",
            _ => OpenFill
        };

        void MetaLine(IContainer container, string label, string value)
        {
            container.Row(row =>
            {
                row.ConstantItem(110).Text(label).SemiBold().FontColor(Muted);
                row.RelativeItem().Text(value).FontColor(Ink);
            });
        }

        void NarrativeCard(IContainer container, string title, string content)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(Colors.White)
                .CornerRadius(14)
                .Padding(10)
                .Column(column =>
                {
                    column.Spacing(5);
                    column.Item().Text(title).Bold().FontSize(10.2f).FontColor(Ink);
                    column.Item().Text(content).FontColor(Ink).LineHeight(1.35f);
                });
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var formLabel = LabelForType(model.FormType);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.4f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(60)
                                            .Height(60)
                                            .Border(1).BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(c => RenderLogo(c, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text(formLabel).SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text(formLabel).SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(230)
                                .Border(1).BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text(model.FormType == StaffConductFormType.Warning ? "WARNING" : "DISCIPLINARY").Bold().FontSize(15).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Form No: {Safe(model.FormNumber)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Issue Date: {model.IssueDate:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Incident Date: {model.IncidentDate:yyyy-MM-dd}");
                                    meta.Item()
                                        .AlignRight()
                                        .Text($"Status: {model.Status}")
                                        .FontColor(model.Status == StaffConductStatus.Resolved ? "#138A5C" : Ink);
                                });
                        });

                        header.Item()
                            .Border(1).BorderColor(Border)
                            .Background(DetailBackground(model.FormType))
                            .CornerRadius(14)
                            .Padding(10)
                            .Column(subject =>
                            {
                                subject.Spacing(4);
                                subject.Item().Text("Subject").SemiBold().FontColor(Muted);
                                subject.Item().Text(Safe(model.Subject)).Bold().FontSize(13).FontColor(Ink);
                            });

                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(profile =>
                                {
                                    profile.Spacing(6);
                                    profile.Item().Text("Staff Profile").Bold().FontSize(10.6f).FontColor(Ink);
                                    profile.Item().Element(c => MetaLine(c, "Staff ID", Safe(model.StaffCode)));
                                    profile.Item().Element(c => MetaLine(c, "Staff Name", Safe(model.StaffName)));
                                    profile.Item().Element(c => MetaLine(c, "Designation", Safe(model.Designation)));
                                    profile.Item().Element(c => MetaLine(c, "Work Site", Safe(model.WorkSite)));
                                    profile.Item().Element(c => MetaLine(c, "ID Number", Safe(model.IdNumber)));
                                });

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(meta =>
                                {
                                    meta.Spacing(6);
                                    meta.Item().Text("Action Summary").Bold().FontSize(10.6f).FontColor(Ink);
                                    meta.Item().Element(c => MetaLine(c, "Severity", model.Severity.ToString()));
                                    meta.Item().Element(c => MetaLine(c, "Issued By", Safe(model.IssuedBy)));
                                    meta.Item().Element(c => MetaLine(c, "Witnessed By", Safe(model.WitnessedBy)));
                                    meta.Item().Element(c => MetaLine(c, "Follow Up", DateValue(model.FollowUpDate)));
                                    meta.Item().Element(c => MetaLine(c, "Acknowledged", model.IsAcknowledgedByStaff ? "Yes" : "No"));
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => NarrativeCard(c, "Incident Details", Safe(model.IncidentDetails)));
                            row.RelativeItem().Element(c => NarrativeCard(c, "Action Taken", Safe(model.ActionTaken)));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => NarrativeCard(c, "Required Improvement", Safe(model.RequiredImprovement)));
                            row.RelativeItem().Element(c => NarrativeCard(c, "Employee Remarks", Safe(model.EmployeeRemarks)));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(StatusBackground(model.Status))
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(status =>
                                {
                                    status.Spacing(6);
                                    status.Item().Text("Status & Resolution").Bold().FontSize(10.6f).FontColor(Ink);
                                    status.Item().Element(c => MetaLine(c, "Current Status", model.Status.ToString()));
                                    status.Item().Element(c => MetaLine(c, "Acknowledged Date", DateValue(model.AcknowledgedDate)));
                                    status.Item().Element(c => MetaLine(c, "Resolved Date", DateValue(model.ResolvedDate)));
                                    status.Item().Element(c => MetaLine(c, "Resolution Notes", Safe(model.ResolutionNotes)));
                                });

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(confirm =>
                                {
                                    confirm.Spacing(6);
                                    confirm.Item().Text("Acknowledgement").Bold().FontSize(10.6f).FontColor(Ink);
                                    confirm.Item().Text(model.IsAcknowledgedByStaff
                                            ? "This form has been acknowledged by the staff member."
                                            : "This form is pending staff acknowledgement.")
                                        .FontColor(Muted);
                                    confirm.Item().Text("Use this record as the single source of truth for follow-up action, escalation, and HR archive.")
                                        .FontColor(Muted);
                                });
                        });
                    });

                    page.Footer().Column(footer =>
                    {
                        footer.Spacing(4);
                        footer.Item().LineHorizontal(1).LineColor(Border);
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru");
                                text.Span(" | ");
                                text.Span($"{generatedAt:yyyy-MM-dd HH:mm} MVT");
                            });

                            row.ConstantItem(72).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildStaffConductFormDhivehiPdf(
        StaffConductDetailDto sourceModel,
        StaffConductDhivehiExportDto dhivehiModel,
        string companyName,
        string companyInfo,
        string? logoUrl)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string Accent = "#6F7FF5";
        const string WarningFill = "#FFF6E7";
        const string DisciplineFill = "#FCECF2";
        const string GoodFill = "#EAF8F1";
        const string OpenFill = "#EEF2FF";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string DateValue(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "-";
        static string LabelForType(StaffConductFormType value) => value == StaffConductFormType.Warning ? "Warning Form" : "Disciplinary Form";
        static string DetailBackground(StaffConductFormType value) => value == StaffConductFormType.Warning ? WarningFill : DisciplineFill;
        static string StatusBackground(StaffConductStatus value) => value switch
        {
            StaffConductStatus.Resolved => GoodFill,
            StaffConductStatus.Acknowledged => "#FFF4DE",
            _ => OpenFill
        };

        var farumaAvailable = EnsureFarumaFontRegistered();
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var formLabel = $"{LabelForType(sourceModel.FormType)} - Dhivehi Export";

        string BuildNarrative(string? dhivehiValue, string? englishFallback)
        {
            return !string.IsNullOrWhiteSpace(dhivehiValue) ? dhivehiValue.Trim() : Safe(englishFallback);
        }

        void MetaLine(IContainer container, string label, string value)
        {
            container.Row(row =>
            {
                row.ConstantItem(118).Text(label).SemiBold().FontColor(Muted);
                row.RelativeItem().Text(value).FontColor(Ink);
            });
        }

        void DhivehiNarrativeCard(IContainer container, string title, string dhivehiLabel, string englishReference, string content, string background)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(background)
                .CornerRadius(14)
                .Padding(10)
                .Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text(title).Bold().FontSize(10.2f).FontColor(Ink);
                    column.Item().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style =>
                        {
                            var next = style.SemiBold().FontSize(9.6f).FontColor(Muted).DirectionFromRightToLeft();
                            return farumaAvailable ? next.FontFamily(FarumaFontFamily) : next;
                        });
                        text.Span(dhivehiLabel);
                    });
                    column.Item().Text($"English reference: {Safe(englishReference)}").FontSize(8.2f).FontColor(Muted);
                    column.Item().Element(body =>
                    {
                        body
                            .ContentFromRightToLeft()
                            .AlignRight()
                            .DefaultTextStyle(style =>
                            {
                                var next = style.FontSize(12f).FontColor(Ink).DirectionFromRightToLeft();
                                return farumaAvailable ? next.FontFamily(FarumaFontFamily) : next;
                            })
                            .Text(content);
                    });
                });
        }

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.4f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(60)
                                            .Height(60)
                                            .Border(1).BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(c => RenderLogo(c, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text(formLabel).SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text(formLabel).SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(232)
                                .Border(1).BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("DHIVEHI PDF").Bold().FontSize(15).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"Form No: {Safe(sourceModel.FormNumber)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Issue Date: {sourceModel.IssueDate:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Incident Date: {sourceModel.IncidentDate:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Status: {sourceModel.Status}");
                                });
                        });

                        header.Item()
                            .Border(1).BorderColor(Border)
                            .Background(DetailBackground(sourceModel.FormType))
                            .CornerRadius(14)
                            .Padding(10)
                            .Column(subject =>
                            {
                                subject.Spacing(5);
                                subject.Item().Text("Subject").SemiBold().FontColor(Muted);
                                subject.Item().AlignRight().Text(text =>
                                {
                                    text.DefaultTextStyle(style =>
                                    {
                                        var next = style.SemiBold().FontSize(9.6f).FontColor(Muted).DirectionFromRightToLeft();
                                        return farumaAvailable ? next.FontFamily(FarumaFontFamily) : next;
                                    });
                                    text.Span("ސަބްޖެކްޓް");
                                });
                                subject.Item().Text($"English reference: {Safe(sourceModel.Subject)}").FontSize(8.2f).FontColor(Muted);
                                subject.Item().Element(body =>
                                {
                                    body
                                        .ContentFromRightToLeft()
                                        .AlignRight()
                                        .DefaultTextStyle(style =>
                                        {
                                            var next = style.FontSize(14f).FontColor(Ink).DirectionFromRightToLeft();
                                            return farumaAvailable ? next.FontFamily(FarumaFontFamily) : next;
                                        })
                                        .Text(BuildNarrative(dhivehiModel.SubjectDv, sourceModel.Subject));
                                });
                            });

                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(Colors.White)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(profile =>
                                {
                                    profile.Spacing(6);
                                    profile.Item().Text("Staff Profile").Bold().FontSize(10.6f).FontColor(Ink);
                                    profile.Item().Element(c => MetaLine(c, "Staff ID", Safe(sourceModel.StaffCode)));
                                    profile.Item().Element(c => MetaLine(c, "Staff Name", Safe(sourceModel.StaffName)));
                                    profile.Item().Element(c => MetaLine(c, "Designation", Safe(sourceModel.Designation)));
                                    profile.Item().Element(c => MetaLine(c, "Work Site", Safe(sourceModel.WorkSite)));
                                    profile.Item().Element(c => MetaLine(c, "ID Number", Safe(sourceModel.IdNumber)));
                                });

                            row.RelativeItem()
                                .Border(1).BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(10)
                                .Column(meta =>
                                {
                                    meta.Spacing(6);
                                    meta.Item().Text("Action Summary").Bold().FontSize(10.6f).FontColor(Ink);
                                    meta.Item().Element(c => MetaLine(c, "Form Type", sourceModel.FormType.ToString()));
                                    meta.Item().Element(c => MetaLine(c, "Severity", sourceModel.Severity.ToString()));
                                    meta.Item().Element(c => MetaLine(c, "Issued By", Safe(sourceModel.IssuedBy)));
                                    meta.Item().Element(c => MetaLine(c, "Witnessed By", Safe(sourceModel.WitnessedBy)));
                                    meta.Item().Element(c => MetaLine(c, "Follow Up", DateValue(sourceModel.FollowUpDate)));
                                });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => DhivehiNarrativeCard(c, "Incident Details", "ހާދިސާގެ ތަފްސީލް", sourceModel.IncidentDetails, BuildNarrative(dhivehiModel.IncidentDetailsDv, sourceModel.IncidentDetails), Colors.White));
                            row.RelativeItem().Element(c => DhivehiNarrativeCard(c, "Action Taken", "ނެގި ފިޔަވަޅު", sourceModel.ActionTaken, BuildNarrative(dhivehiModel.ActionTakenDv, sourceModel.ActionTaken), Colors.White));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => DhivehiNarrativeCard(c, "Required Improvement", "ބޭނުންވާ އިސްލާހު", sourceModel.RequiredImprovement ?? "-", BuildNarrative(dhivehiModel.RequiredImprovementDv, sourceModel.RequiredImprovement), Colors.White));
                            row.RelativeItem().Element(c => DhivehiNarrativeCard(c, "Employee Remarks", "މުވައްޒަފުގެ ރިމާކްސް", sourceModel.EmployeeRemarks ?? "-", BuildNarrative(dhivehiModel.EmployeeRemarksDv, sourceModel.EmployeeRemarks), Colors.White));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem().Element(c => DhivehiNarrativeCard(
                                c,
                                "Acknowledgement",
                                "އެކްނޮލެޖްމަންޓް",
                                dhivehiModel.AcknowledgementSource,
                                BuildNarrative(dhivehiModel.AcknowledgementDv, dhivehiModel.AcknowledgementSource),
                                StatusBackground(sourceModel.Status)));

                            row.RelativeItem().Element(c => DhivehiNarrativeCard(
                                c,
                                "Resolution Notes",
                                "ނިންމުމުގެ ނޯޓްސް",
                                sourceModel.ResolutionNotes ?? "-",
                                BuildNarrative(dhivehiModel.ResolutionNotesDv, sourceModel.ResolutionNotes),
                                StatusBackground(sourceModel.Status)));
                        });
                    });

                    page.Footer().Column(footer =>
                    {
                        footer.Spacing(4);
                        footer.Item().LineHorizontal(1).LineColor(Border);
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru");
                                text.Span(" | ");
                                text.Span($"{generatedAt:yyyy-MM-dd HH:mm} MVT");
                            });

                            row.ConstantItem(72).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildStaffConductSummaryPdf(IReadOnlyList<StaffConductListItemDto> rows, StaffConductSummaryDto summary, string companyName, string companyInfo, string? logoUrl, StaffConductListQuery query)
    {
        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        static string FilterLabel(StaffConductListQuery value)
        {
            var parts = new List<string>();
            if (value.FormType.HasValue)
            {
                parts.Add($"Type: {value.FormType.Value}");
            }

            if (value.Status.HasValue)
            {
                parts.Add($"Status: {value.Status.Value}");
            }

            if (value.DateFrom.HasValue || value.DateTo.HasValue)
            {
                parts.Add($"Range: {(value.DateFrom?.ToString("yyyy-MM-dd") ?? "Start")} to {(value.DateTo?.ToString("yyyy-MM-dd") ?? "Today")}");
            }

            if (!string.IsNullOrWhiteSpace(value.Search))
            {
                parts.Add($"Search: {value.Search.Trim()}");
            }

            return parts.Count == 0 ? "All staff conduct forms" : string.Join(" | ", parts);
        }

        void Metric(IContainer container, string label, string value, string detail, string background)
        {
            container
                .Border(1).BorderColor(Border)
                .Background(background)
                .CornerRadius(14)
                .Padding(10)
                .Column(metric =>
                {
                    metric.Spacing(3);
                    metric.Item().Text(label).SemiBold().FontSize(8.2f).FontColor(Muted);
                    metric.Item().Text(value).Bold().FontSize(12).FontColor(Ink);
                    metric.Item().Text(detail).FontSize(8f).FontColor(Muted);
                });
        }

        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var generatedAt = DateTimeOffset.UtcNow.ToOffset(MaldivesOffset);
        var filterLabel = FilterLabel(query);
        var rowsData = rows.Select(item => new[]
        {
            item.FormNumber,
            item.FormType.ToString(),
            item.IssueDate.ToString("yyyy-MM-dd"),
            item.StaffCode,
            item.StaffName,
            Safe(item.Designation),
            item.Subject,
            item.Severity.ToString(),
            item.Status.ToString(),
            item.IssuedBy
        }).ToList();

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9.1f).FontColor(Ink));

                    page.Header().Column(header =>
                    {
                        header.Spacing(8);
                        header.Item().Row(row =>
                        {
                            row.Spacing(12);

                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(62)
                                            .Height(62)
                                            .Border(1).BorderColor(Border)
                                            .Background(Colors.White)
                                            .CornerRadius(12)
                                            .Padding(5)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(c => RenderLogo(c, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Spacing(2);
                                            text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                            text.Item().Text("Staff Conduct Register").SemiBold().FontSize(10.6f).FontColor(Accent);
                                            text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Spacing(2);
                                        text.Item().Text(companyName).Bold().FontSize(16).FontColor(Ink);
                                        text.Item().Text("Staff Conduct Register").SemiBold().FontSize(10.6f).FontColor(Accent);
                                        text.Item().Text(companyInfo).FontSize(8.4f).FontColor(Muted);
                                    });
                                }
                            });

                            row.ConstantItem(310)
                                .Border(1).BorderColor(Border)
                                .Background(Panel)
                                .CornerRadius(14)
                                .Padding(12)
                                .Column(meta =>
                                {
                                    meta.Spacing(4);
                                    meta.Item().AlignRight().Text("SUMMARY EXPORT").Bold().FontSize(15).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"{rows.Count:N0} form(s)").SemiBold();
                                    meta.Item().AlignRight().Text(filterLabel).FontSize(8.2f).FontColor(Muted);
                                    meta.Item().AlignRight().Text($"Generated: {generatedAt:yyyy-MM-dd HH:mm} MVT").FontColor(Muted);
                                });
                        });

                        AddHeaderDivider(header, Border);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => Metric(c, "Total Forms", summary.TotalForms.ToString(), "All matching records", Colors.White));
                            row.RelativeItem().Element(c => Metric(c, "Warnings", summary.WarningCount.ToString(), "Warning forms", "#FFF6E7"));
                            row.RelativeItem().Element(c => Metric(c, "Disciplinary", summary.DisciplinaryCount.ToString(), "Disciplinary forms", "#FCECF2"));
                            row.RelativeItem().Element(c => Metric(c, "Open", summary.OpenCount.ToString(), "Pending closure", "#EEF2FF"));
                            row.RelativeItem().Element(c => Metric(c, "Resolved", summary.ResolvedCount.ToString(), "Closed cases", "#EAF8F1"));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(92);
                                columns.ConstantColumn(88);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(74);
                                columns.RelativeColumn(1.3f);
                                columns.ConstantColumn(100);
                                columns.RelativeColumn(1.6f);
                                columns.ConstantColumn(72);
                                columns.ConstantColumn(100);
                                columns.ConstantColumn(100);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Border).Background(HeaderFill).Padding(5);
                            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(Border).Background(background).Padding(5);

                            table.Header(header =>
                            {
                                foreach (var title in new[] { "Form No", "Type", "Issue Date", "Staff ID", "Staff Name", "Designation", "Subject", "Severity", "Status", "Issued By" })
                                {
                                    header.Cell().Element(HeaderCell).Text(title).Bold().FontColor(Ink);
                                }
                            });

                            if (rowsData.Count == 0)
                            {
                                table.Cell()
                                    .ColumnSpan(10)
                                    .Element(c => BodyCell(c, Colors.White))
                                    .AlignCenter()
                                    .Text("No staff conduct forms matched the selected filter.")
                                    .FontColor(Muted);
                            }
                            else
                            {
                                for (var index = 0; index < rowsData.Count; index++)
                                {
                                    var background = index % 2 == 0 ? "#FFFFFF" : Panel;
                                    foreach (var value in rowsData[index])
                                    {
                                        table.Cell().Element(c => BodyCell(c, background)).Text(value).FontColor(Ink);
                                    }
                                }
                            }
                        });
                    });

                    page.Footer().Column(footer =>
                    {
                        footer.Spacing(4);
                        footer.Item().LineHorizontal(1).LineColor(Border);
                        footer.Item().Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.Span("Generated by myDhathuru");
                                text.Span(" | ");
                                text.Span($"{generatedAt:yyyy-MM-dd HH:mm} MVT");
                            });

                            row.ConstantItem(72).AlignRight().Text(text =>
                            {
                                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildMiraInputTaxStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl)
    {
        var statement = model.InputTaxStatement ?? throw new InvalidOperationException("Input tax statement preview is missing.");
        var rows = statement.Rows.Select((item, index) => new[]
        {
            (index + 1).ToString(),
            item.SupplierTin,
            item.SupplierName,
            item.SupplierInvoiceNumber,
            item.InvoiceDate.ToString("yyyy-MM-dd"),
            item.InvoiceTotalExcludingGst.ToString("N2"),
            item.GstChargedAt6.ToString("N2"),
            item.GstChargedAt8.ToString("N2"),
            item.GstChargedAt12.ToString("N2"),
            item.GstChargedAt16.ToString("N2"),
            item.GstChargedAt17.ToString("N2"),
            item.TotalGst.ToString("N2"),
            item.TaxableActivityNumber,
            item.RevenueCapitalClassification.ToString()
        }).ToList();

        var metrics = new[]
        {
            ("Invoices", statement.TotalInvoices.ToString()),
            ("Taxable Base", statement.TotalInvoiceBase.ToString("N2")),
            ("Claimable GST", statement.TotalClaimableGst.ToString("N2")),
            ("Activity", model.Meta.TaxableActivityNumber)
        };

        return BuildMiraTabularPdf(
            companyName,
            companyInfo,
            logoUrl,
            model,
            "MIRA Input Tax Statement",
            metrics,
            new[] { "#", "Supplier TIN", "Supplier Name", "Invoice No.", "Invoice Date", "Taxable Value", "GST 6%", "GST 8%", "GST 12%", "GST 16%", "GST 17%", "Total GST", "Activity No.", "Revenue / Capital" },
            rows);
    }

    public byte[] BuildMiraOutputTaxStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl)
    {
        var statement = model.OutputTaxStatement ?? throw new InvalidOperationException("Output tax statement preview is missing.");
        var rows = statement.Rows.Select((item, index) => new[]
        {
            (index + 1).ToString(),
            item.CustomerTin,
            item.CustomerName,
            item.InvoiceNo,
            item.InvoiceDate.ToString("yyyy-MM-dd"),
            item.TaxableSupplies.ToString("N2"),
            item.ZeroRatedSupplies.ToString("N2"),
            item.ExemptSupplies.ToString("N2"),
            item.OutOfScopeSupplies.ToString("N2"),
            item.GstRate.ToString("N2"),
            item.GstAmount.ToString("N2")
        }).ToList();

        var metrics = new[]
        {
            ("Invoices", statement.TotalInvoices.ToString()),
            ("Taxable", statement.TotalTaxableSupplies.ToString("N2")),
            ("Out of Scope", statement.TotalOutOfScopeSupplies.ToString("N2")),
            ("GST", statement.TotalTaxAmount.ToString("N2"))
        };

        return BuildMiraTabularPdf(
            companyName,
            companyInfo,
            logoUrl,
            model,
            "MIRA Output Tax Statement",
            metrics,
            new[] { "#", "Customer TIN", "Customer", "Invoice No.", "Invoice Date", "Taxable", "Zero Rated", "Exempt", "Out of Scope", "GST Rate", "GST Amount" },
            rows);
    }

    public byte[] BuildBptIncomeStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl, string? companyStampUrl, string? companySignatureUrl)
    {
        var statement = model.BptIncomeStatement ?? throw new InvalidOperationException("BPT income statement preview is missing.");
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var stampAsset = ResolveTenantSupportingImage(companyStampUrl, "Unable to load company stamp for BPT PDF rendering.");
        var signatureAsset = ResolveTenantSupportingImage(companySignatureUrl, "Unable to load company signature for BPT PDF rendering.");

        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string Accent = "#6F7FF5";
        const string GoodFill = "#E9F9F2";

        static string Money(decimal amount) => $"MVR {amount:N2}";

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink));

                    page.Header().Element(c => ComposeMiraHeader(c, logoAsset, companyName, companyInfo, model, "BPT Income Statement", Border, Panel, Ink, Muted, Accent));

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);
                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            row.RelativeItem().Element(c => MetricCard(c, "Net Sales", Money(statement.NetSales), Panel, Border, Ink, Muted));
                            row.RelativeItem().Element(c => MetricCard(c, "Gross Profit", Money(statement.GrossProfit), "#EEF7FF", Border, Ink, Muted));
                            row.RelativeItem().Element(c => MetricCard(c, "Operating Expenses", Money(statement.TotalOperatingExpenses), "#FFF7EA", Border, Ink, Muted));
                            row.RelativeItem().Element(c => MetricCard(c, "Net Income", Money(statement.NetIncome), GoodFill, Border, Ink, Muted));
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(10);

                            row.RelativeItem()
                                .Border(1).BorderColor(Border).Background(Colors.White).CornerRadius(14).Padding(12)
                                .Column(left =>
                                {
                                    left.Spacing(6);
                                    left.Item().Text("Income Statement").Bold().FontSize(12).FontColor(Ink);
                                    left.Item().Element(c => SummaryLine(c, "Gross sales", Money(statement.GrossSales), Border, Ink, Muted));
                                    left.Item().Element(c => SummaryLine(c, "Sales returns and allowances", Money(statement.SalesReturnsAndAllowances), Border, Ink, Muted));
                                    left.Item().Element(c => SummaryLine(c, "Net sales", Money(statement.NetSales), Border, Ink, Ink, true));
                                    left.Item().Element(c => SummaryLine(c, "Cost of goods sold", Money(statement.CostOfGoodsSold), Border, Ink, Muted));
                                    left.Item().Element(c => SummaryLine(c, "Gross profit", Money(statement.GrossProfit), Border, Ink, Ink, true));
                                    left.Item().Element(c => SummaryLine(c, "Total operating expenses", Money(statement.TotalOperatingExpenses), Border, Ink, Muted));
                                    left.Item().Element(c => SummaryLine(c, "Net operating income", Money(statement.NetOperatingIncome), Border, Ink, Ink, true));
                                    left.Item().Element(c => SummaryLine(c, "Total other income", Money(statement.TotalOtherIncome), Border, Ink, Muted));
                                    left.Item().Element(c => SummaryLine(c, "Net income", Money(statement.NetIncome), Border, "#1F4E43", "#1F4E43", true, GoodFill));
                                });

                            row.RelativeItem()
                                .Border(1).BorderColor(Border).Background(Colors.White).CornerRadius(14).Padding(12)
                                .Column(right =>
                                {
                                    right.Spacing(6);
                                    right.Item().Text("Operating Expense Breakdown").Bold().FontSize(12).FontColor(Ink);
                                    if (statement.OperatingExpenses.Count == 0)
                                    {
                                        right.Item().Text("No operating expense lines for the selected period.").FontColor(Muted);
                                    }
                                    else
                                    {
                                        foreach (var item in statement.OperatingExpenses)
                                        {
                                            right.Item().Element(c => SummaryLine(c, item.Label, Money(item.Amount), Border, Ink, Muted));
                                        }
                                    }
                                });
                        });

                        if (stampAsset?.HasValue == true || signatureAsset?.HasValue == true)
                        {
                            column.Item().Element(c => BuildBptApprovalSection(c, stampAsset, signatureAsset, Border));
                        }
                    });

                    page.Footer().Element(c => ComposeMiraFooter(c, model.Meta, Muted));
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildBptNotesPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl, string? companyStampUrl, string? companySignatureUrl)
    {
        var notes = model.BptNotes ?? throw new InvalidOperationException("BPT notes preview is missing.");
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);
        var stampAsset = ResolveTenantSupportingImage(companyStampUrl, "Unable to load company stamp for BPT notes PDF rendering.");
        var signatureAsset = ResolveTenantSupportingImage(companySignatureUrl, "Unable to load company signature for BPT notes PDF rendering.");

        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9.2f).FontColor(Ink));

                    page.Header().Element(c => ComposeMiraHeader(c, logoAsset, companyName, companyInfo, model, "BPT Notes", Border, Panel, Ink, Muted, Accent));
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Element(c => BuildSalaryNotesTable(c, notes, Border, HeaderFill, Panel, Ink, Muted));

                        foreach (var section in notes.Sections)
                        {
                            column.Item()
                                .Border(1).BorderColor(Border).Background(Colors.White).CornerRadius(14).Padding(10)
                                .Column(block =>
                                {
                                    block.Spacing(6);
                                    block.Item().Row(row =>
                                    {
                                        row.RelativeItem().Text(section.Title).Bold().FontSize(11.5f).FontColor(Ink);
                                        row.ConstantItem(120).AlignRight().Text($"Total: MVR {section.TotalAmount:N2}").Bold().FontColor(Ink);
                                    });
                                    block.Item().Element(c => BuildExpenseNotesTable(c, section, Border, HeaderFill, Panel, Ink, Muted));
                                });
                        }

                        if (stampAsset?.HasValue == true || signatureAsset?.HasValue == true)
                        {
                            column.Item().Element(c => BuildBptApprovalSection(c, stampAsset, signatureAsset, Border));
                        }
                    });

                    page.Footer().Element(c => ComposeMiraFooter(c, model.Meta, Muted));
                });
            })
            .GeneratePdf();
    }

    private byte[] BuildMiraTabularPdf(
        string companyName,
        string companyInfo,
        string? logoUrl,
        MiraReportPreviewDto model,
        string subtitle,
        IReadOnlyList<(string Label, string Value)> metrics,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows)
    {
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);

        const string Ink = "#283B63";
        const string Muted = "#697DA7";
        const string Border = "#D8E2F4";
        const string Panel = "#F7FAFF";
        const string HeaderFill = "#EEF3FF";
        const string Accent = "#6F7FF5";

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(8.9f).FontColor(Ink));

                    page.Header().Element(c => ComposeMiraHeader(c, logoAsset, companyName, companyInfo, model, subtitle, Border, Panel, Ink, Muted, Accent));
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Row(row =>
                        {
                            row.Spacing(8);
                            foreach (var metric in metrics)
                            {
                                row.RelativeItem().Element(c => MetricCard(c, metric.Label, metric.Value, Panel, Border, Ink, Muted));
                            }
                        });

                        column.Item()
                            .Border(1).BorderColor(Border).Background(Colors.White).CornerRadius(14).Padding(10)
                            .Column(body =>
                            {
                                body.Spacing(6);
                                body.Item().Text("Statement lines").Bold().FontSize(11.5f).FontColor(Ink);
                                body.Item().Element(c => BuildSimpleTable(c, headers, rows, Border, HeaderFill, Panel, Ink, Muted));
                            });

                        if (model.Assumptions.Count > 0)
                        {
                            column.Item()
                                .Border(1).BorderColor(Border).Background(HeaderFill).CornerRadius(14).Padding(10)
                                .Column(notes =>
                                {
                                    notes.Spacing(4);
                                    notes.Item().Text("Important Filing Notes").Bold().FontColor(Ink);
                                    foreach (var item in model.Assumptions)
                                    {
                                        notes.Item().Text($"• {item}").FontColor(Muted);
                                    }
                                });
                        }
                    });

                    page.Footer().Element(c => ComposeMiraFooter(c, model.Meta, Muted));
                });
            })
            .GeneratePdf();
    }

    private static void AddHeaderDivider(ColumnDescriptor header, string borderColor)
    {
        header.Item()
            .PaddingTop(2)
            .PaddingBottom(6)
            .LineHorizontal(1)
            .LineColor(borderColor);
    }

    private static void ComposeMiraHeader(
        IContainer container,
        LogoAsset? logoAsset,
        string companyName,
        string companyInfo,
        MiraReportPreviewDto model,
        string subtitle,
        string border,
        string panel,
        string ink,
        string muted,
        string accent)
    {
        container.Column(header =>
        {
            header.Spacing(8);
            header.Item().Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Element(left =>
                {
                    if (logoAsset?.HasValue == true)
                    {
                        left.Row(brand =>
                        {
                            brand.Spacing(8);
                            brand.ConstantItem(58)
                                .Height(58)
                                .Border(1).BorderColor(border)
                                .Background(Colors.White)
                                .CornerRadius(12)
                                .Padding(5)
                                .AlignCenter()
                                .AlignMiddle()
                                .Element(c => RenderLogo(c, logoAsset));
                            brand.RelativeItem().Column(text =>
                            {
                                text.Spacing(2);
                                text.Item().Text(companyName).Bold().FontSize(15).FontColor(ink);
                                text.Item().Text(subtitle).SemiBold().FontSize(10.4f).FontColor(accent);
                                text.Item().Text(companyInfo).FontSize(8.4f).FontColor(muted);
                            });
                        });
                    }
                    else
                    {
                        left.Column(text =>
                        {
                            text.Spacing(2);
                            text.Item().Text(companyName).Bold().FontSize(15).FontColor(ink);
                            text.Item().Text(subtitle).SemiBold().FontSize(10.4f).FontColor(accent);
                            text.Item().Text(companyInfo).FontSize(8.4f).FontColor(muted);
                        });
                    }
                });

                row.ConstantItem(240)
                    .Border(1).BorderColor(border).Background(panel).CornerRadius(14).Padding(12)
                    .Column(meta =>
                    {
                        meta.Spacing(4);
                        meta.Item().AlignRight().Text(model.Meta.Title).Bold().FontSize(14).FontColor(ink);
                        meta.Item().AlignRight().Text($"Period: {model.Meta.PeriodLabel}").SemiBold().FontColor(ink);
                        meta.Item().AlignRight().Text($"Range: {model.Meta.RangeStart:yyyy-MM-dd} to {model.Meta.RangeEnd:yyyy-MM-dd}").FontColor(muted);
                        meta.Item().AlignRight().Text($"Activity No.: {model.Meta.TaxableActivityNumber}").FontColor(muted);
                    });
            });

            AddHeaderDivider(header, border);
        });
    }

    private static void ComposeMiraFooter(IContainer container, MiraReportMetaDto meta, string muted)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(muted));
                text.Span("Generated by myDhathuru");
                text.Span(" | ");
                text.Span($"{ToMaldivesTime(meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT");
            });

            row.ConstantItem(54).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(muted));
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void MetricCard(IContainer container, string label, string value, string background, string border, string ink, string muted)
    {
        container
            .Border(1).BorderColor(border)
            .Background(background)
            .CornerRadius(12)
            .PaddingVertical(8)
            .PaddingHorizontal(10)
            .Column(metric =>
            {
                metric.Spacing(3);
                metric.Item().Text(label.ToUpperInvariant()).SemiBold().FontSize(7.6f).FontColor(muted);
                metric.Item().Text(value).Bold().FontSize(11.5f).FontColor(ink);
            });
    }

    private static void SummaryLine(IContainer container, string label, string value, string border, string ink, string muted, bool emphasize = false, string? background = null)
    {
        container
            .BorderBottom(1).BorderColor(border)
            .Background(background ?? Colors.Transparent)
            .PaddingVertical(6)
            .Row(row =>
            {
                row.RelativeItem().Text(label).SemiBold().FontColor(emphasize ? ink : muted);
                row.ConstantItem(130).AlignRight().Text(value).Bold().FontColor(ink);
            });
    }

    private static void BuildSimpleTable(IContainer container, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string border, string headerFill, string panel, string ink, string muted)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var _ in headers)
                {
                    columns.RelativeColumn();
                }
            });

            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(border).Background(headerFill).Padding(5);
            IContainer BodyCell(IContainer c, string background) => c.Border(1).BorderColor(border).Background(background).Padding(5);

            table.Header(header =>
            {
                foreach (var item in headers)
                {
                    header.Cell().Element(HeaderCell).Text(item).Bold().FontColor(ink);
                }
            });

            if (rows.Count == 0)
            {
                table.Cell().ColumnSpan((uint)headers.Count)
                    .Element(c => BodyCell(c, Colors.White))
                    .AlignCenter()
                    .Text("No records found for the selected period.")
                    .FontColor(muted);
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var background = i % 2 == 0 ? "#FFFFFF" : panel;
                foreach (var cell in rows[i])
                {
                    table.Cell().Element(c => BodyCell(c, background)).Text(cell).FontColor(ink);
                }
            }
        });
    }

    private static void BuildSalaryNotesTable(IContainer container, BptNotesDto notes, string border, string headerFill, string panel, string ink, string muted)
    {
        container
            .Border(1).BorderColor(border).Background(Colors.White).CornerRadius(14).Padding(10)
            .Column(block =>
            {
                block.Spacing(6);
                block.Item().Row(row =>
                {
                    row.RelativeItem().Text("Salary details").Bold().FontSize(11.5f).FontColor(ink);
                    row.ConstantItem(120).AlignRight().Text($"Total: MVR {notes.TotalSalary:N2}").Bold().FontColor(ink);
                });
                block.Item().Element(c => BuildSimpleTable(
                    c,
                    new[] { "Staff ID", "Staff Name", "Avg Basic", "Avg Allowances", "Periods", "Total" },
                    notes.SalaryRows.Select(x => new[]
                    {
                        x.StaffCode,
                        x.StaffName,
                        x.AverageBasicPerPeriod.ToString("N2"),
                        x.AverageAllowancePerPeriod.ToString("N2"),
                        x.PeriodCount.ToString(),
                        x.TotalForPeriodRange.ToString("N2")
                    }).ToList(),
                    border,
                    headerFill,
                    panel,
                    ink,
                    muted));
            });
    }

    private static void BuildExpenseNotesTable(IContainer container, BptExpenseNoteSectionDto section, string border, string headerFill, string panel, string ink, string muted)
    {
        BuildSimpleTable(
            container,
            new[] { "Date", "Document Number", "Payee", "Detail", "Amount" },
            section.Rows.Select(x => new[]
            {
                x.Date.ToString("yyyy-MM-dd"),
                x.DocumentNumber,
                x.PayeeName,
                x.Detail,
                x.Amount.ToString("N2")
            }).ToList(),
            border,
            headerFill,
            panel,
            ink,
            muted);
    }

    private static void BuildBptApprovalSection(IContainer container, LogoAsset? stampAsset, LogoAsset? signatureAsset, string border)
    {
        var hasStamp = stampAsset?.HasValue == true;
        var hasSignature = signatureAsset?.HasValue == true;
        if (!hasStamp && !hasSignature)
        {
            return;
        }

        container
            .PaddingTop(2)
            .Row(row =>
            {
                row.Spacing(10);

                if (hasStamp)
                {
                    row.ConstantItem(146).Element(c => BuildBptApprovalAssetCard(c, stampAsset!, border));
                }

                if (hasSignature)
                {
                    row.ConstantItem(182).Element(c => BuildBptApprovalAssetCard(c, signatureAsset!, border));
                }
            });
    }

    private static void BuildBptApprovalAssetCard(IContainer container, LogoAsset asset, string border)
    {
        container
            .Border(1).BorderColor(border).Background(Colors.White).CornerRadius(10).Padding(6)
            .Height(68)
            .AlignCenter()
            .AlignMiddle()
            .Element(c => RenderLogo(c, asset));
    }


    private static string BuildMetaLine(ReportMetaDto meta)
    {
        return
            $"Preset: {meta.DatePreset} | Range: {ToMaldivesTime(meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(meta.RangeEndUtc):yyyy-MM-dd HH:mm} MVT | Customer: {meta.CustomerFilterLabel} | Generated: {ToMaldivesTime(meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT";
    }

    private bool EnsureFarumaFontRegistered()
    {
        if (FarumaFontRegistrationAttempted)
        {
            return FarumaFontRegistered;
        }

        lock (FarumaFontSync)
        {
            if (FarumaFontRegistrationAttempted)
            {
                return FarumaFontRegistered;
            }

            foreach (var candidatePath in GetFarumaFontCandidatePaths())
            {
                try
                {
                    var normalizedPath = Path.GetFullPath(candidatePath);
                    if (!File.Exists(normalizedPath))
                    {
                        continue;
                    }

                    using var stream = File.OpenRead(normalizedPath);
                    QuestPDF.Drawing.FontManager.RegisterFontWithCustomName(FarumaFontFamily, stream);
                    FarumaFontRegistered = true;
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Unable to register Faruma font from {FontPath}.", candidatePath);
                }
            }

            FarumaFontRegistrationAttempted = true;

            if (!FarumaFontRegistered)
            {
                _logger.LogWarning("Faruma font was not found for Dhivehi staff-conduct exports. Falling back to the default QuestPDF font.");
            }

            return FarumaFontRegistered;
        }
    }

    private IEnumerable<string> GetFarumaFontCandidatePaths()
    {
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "Assets", "Fonts", "Faruma.ttf");
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "Assets", "Fonts", "faruma.ttf");
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Faruma.ttf");
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "faruma.ttf");
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", "..", "MyDhathuru.Api", "Assets", "Fonts", "Faruma.ttf");
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", "..", "MyDhathuru.Api", "Assets", "Fonts", "faruma.ttf");
    }

    private LogoAsset? ResolvePortalAdminLogo(string? logoUrl)
    {
        return ResolveLogoAsset(
            logoUrl,
            includeTenantUploads: false,
            fallbackLogoFileName: DefaultAppLogoFileName,
            warningMessage: "Unable to load portal admin billing logo for PDF rendering.");
    }

    private LogoAsset? ResolveTenantInvoiceLogo(string? logoUrl)
    {
        return ResolveLogoAsset(
            logoUrl,
            includeTenantUploads: true,
            fallbackLogoFileName: DefaultInvoiceLogoFileName,
            warningMessage: "Unable to load tenant invoice logo for PDF rendering.");
    }

    private LogoAsset? ResolveTenantSupportingImage(string? imageUrl, string warningMessage)
    {
        return ResolveLogoAsset(
            imageUrl,
            includeTenantUploads: true,
            fallbackLogoFileName: string.Empty,
            warningMessage: warningMessage);
    }

    private LogoAsset? ResolveLogoAsset(
        string? logoUrl,
        bool includeTenantUploads,
        string fallbackLogoFileName,
        string warningMessage)
    {
        TryReadBrandLogoAssetByFileName(fallbackLogoFileName, out var fallbackLogoAsset);

        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return fallbackLogoAsset;
        }

        var candidate = logoUrl.Trim();
        try
        {
            if (TryDecodeDataUri(candidate, out var dataUriBytes))
            {
                return BuildLogoAsset(dataUriBytes, candidate) ?? fallbackLogoAsset;
            }

            if (TryReadBrandLogoAsset(candidate, out var brandLogoAsset))
            {
                return brandLogoAsset;
            }

            if (includeTenantUploads && TryReadTenantLogoFromUploadsPath(candidate, out var tenantFileBytes))
            {
                return BuildLogoAsset(tenantFileBytes, candidate) ?? fallbackLogoAsset;
            }

            if (TryReadLogoFromUploadsPath(candidate, out var adminFileBytes))
            {
                return BuildLogoAsset(adminFileBytes, candidate) ?? fallbackLogoAsset;
            }

            if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.Scheme == Uri.UriSchemeFile)
                {
                    return TryReadLogoAssetFromFile(absoluteUri.LocalPath, out var localFileAsset)
                        ? localFileAsset
                        : fallbackLogoAsset;
                }

                if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                {
                    if (TryReadBrandLogoAsset(absoluteUri.AbsolutePath, out var absoluteBrandLogoAsset))
                    {
                        return absoluteBrandLogoAsset;
                    }

                    if (includeTenantUploads && TryReadTenantLogoFromUploadsPath(absoluteUri.AbsolutePath, out var localTenantUploadBytes))
                    {
                        return BuildLogoAsset(localTenantUploadBytes, absoluteUri.AbsolutePath) ?? fallbackLogoAsset;
                    }

                    if (TryReadLogoFromUploadsPath(absoluteUri.AbsolutePath, out var localAdminUploadBytes))
                    {
                        return BuildLogoAsset(localAdminUploadBytes, absoluteUri.AbsolutePath) ?? fallbackLogoAsset;
                    }

                    return TryReadRemoteLogoAsset(absoluteUri) ?? fallbackLogoAsset;
                }
            }

            return TryReadLogoAssetFromFile(candidate, out var directFileAsset)
                ? directFileAsset
                : fallbackLogoAsset;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, warningMessage);
            return fallbackLogoAsset;
        }
    }

    private static void RenderLogo(IContainer container, LogoAsset logoAsset)
    {
        if (!string.IsNullOrWhiteSpace(logoAsset.SvgMarkup))
        {
            container.Svg(logoAsset.SvgMarkup!);
            return;
        }

        if (logoAsset.ImageBytes is { Length: > 0 } imageBytes)
        {
            container.Image(imageBytes).FitArea();
        }
    }

    private bool TryReadLogoFromUploadsPath(string pathOrUrl, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var logoDirectory = GetPortalAdminLogoDirectory();
        if (string.IsNullOrWhiteSpace(logoDirectory))
        {
            return false;
        }

        var normalized = pathOrUrl.Split('?', 2)[0].Split('#', 2)[0].Trim().Replace('\\', '/');
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = normalized.TrimStart('/');
        if (!relativePath.StartsWith("uploads/logos/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var filePath = Path.Combine(logoDirectory, fileName);
        return TryReadFileBytes(filePath, out bytes);
    }

    private bool TryReadTenantLogoFromUploadsPath(string pathOrUrl, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        var normalized = pathOrUrl.Split('?', 2)[0].Split('#', 2)[0].Trim().Replace('\\', '/');
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = normalized.TrimStart('/');
        foreach (var folderName in new[] { "company-logos", "company-stamps", "company-signatures" })
        {
            var prefix = $"uploads/{folderName}/";
            if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var childPath = relativePath[prefix.Length..];
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return false;
            }

            return TryReadRelativeFileUnderRoot(GetTenantUploadRootDirectory(folderName), childPath, out bytes);
        }

        return false;
    }

    private bool TryReadFileBytes(string filePath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            var normalizedFilePath = Path.GetFullPath(filePath);
            if (!File.Exists(normalizedFilePath))
            {
                return false;
            }

            var fileInfo = new FileInfo(normalizedFilePath);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaxPortalAdminLogoBytes)
            {
                return false;
            }

            var fileBytes = File.ReadAllBytes(normalizedFilePath);
            if (fileBytes.Length == 0 || fileBytes.Length > MaxPortalAdminLogoBytes)
            {
                return false;
            }

            bytes = fileBytes;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryReadLogoAssetFromFile(string filePath, out LogoAsset? logoAsset)
    {
        logoAsset = null;
        if (!TryReadFileBytes(filePath, out var fileBytes))
        {
            return false;
        }

        logoAsset = BuildLogoAsset(fileBytes, filePath);
        return logoAsset?.HasValue == true;
    }

    private LogoAsset? TryReadRemoteLogoAsset(Uri logoUri)
    {
        try
        {
            using var response = LogoHttpClient.GetAsync(logoUri, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxPortalAdminLogoBytes)
            {
                return null;
            }

            var logoBytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (logoBytes.Length == 0 || logoBytes.Length > MaxPortalAdminLogoBytes)
            {
                return null;
            }

            var sourceHint = response.Content.Headers.ContentType?.MediaType ?? logoUri.AbsolutePath;
            return BuildLogoAsset(logoBytes, sourceHint);
        }
        catch
        {
            return null;
        }
    }

    private bool TryReadBrandLogoAsset(string pathOrUrl, out LogoAsset? logoAsset)
    {
        logoAsset = null;
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return false;
        }

        var normalized = pathOrUrl.Split('?', 2)[0].Split('#', 2)[0].Trim();
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
        {
            normalized = absoluteUri.AbsolutePath;
        }

        var fileName = Path.GetFileName(normalized.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return TryReadBrandLogoAssetByFileName(fileName, out logoAsset);
    }

    private bool TryReadBrandLogoAssetByFileName(string fileName, out LogoAsset? logoAsset)
    {
        logoAsset = null;

        if (!fileName.Equals(DefaultInvoiceLogoFileName, StringComparison.OrdinalIgnoreCase)
            && !fileName.Equals(DefaultAppLogoFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var candidatePath in GetBrandLogoCandidatePaths(fileName))
        {
            if (TryReadLogoAssetFromFile(candidatePath, out logoAsset))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetBrandLogoCandidatePaths(string fileName)
    {
        var webRootPath = string.IsNullOrWhiteSpace(_hostEnvironment.WebRootPath)
            ? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot")
            : _hostEnvironment.WebRootPath;

        yield return Path.Combine(webRootPath, fileName);
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot", fileName);
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", "..", "frontend", "public", fileName);
        yield return Path.Combine(_hostEnvironment.ContentRootPath, "..", "..", "frontend", "public", fileName);
    }

    private LogoAsset? BuildLogoAsset(byte[] rawBytes, string sourceHint)
    {
        if (rawBytes.Length == 0 || rawBytes.Length > MaxPortalAdminLogoBytes)
        {
            return null;
        }

        if (LooksLikeSvg(sourceHint, rawBytes, out var svgMarkup))
        {
            return new LogoAsset { SvgMarkup = svgMarkup };
        }

        return new LogoAsset { ImageBytes = rawBytes };
    }

    private static bool LooksLikeSvg(string sourceHint, byte[] rawBytes, out string? svgMarkup)
    {
        svgMarkup = null;
        var extension = Path.GetExtension(sourceHint).Trim();
        var mayBeSvg = extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);

        if (!mayBeSvg)
        {
            var prefix = rawBytes.Length >= 5
                ? System.Text.Encoding.UTF8.GetString(rawBytes, 0, Math.Min(rawBytes.Length, 256))
                : string.Empty;
            mayBeSvg = prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
        }

        if (!mayBeSvg)
        {
            return false;
        }

        var decoded = System.Text.Encoding.UTF8.GetString(rawBytes);
        if (!decoded.Contains("<svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        svgMarkup = decoded;
        return true;
    }

    private static bool TryDecodeDataUri(string input, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!input.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commaIndex = input.IndexOf(',');
        if (commaIndex <= 0)
        {
            return false;
        }

        var header = input[..commaIndex];
        if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var rawBytes = Convert.FromBase64String(input[(commaIndex + 1)..]);
            if (rawBytes.Length == 0 || rawBytes.Length > MaxPortalAdminLogoBytes)
            {
                return false;
            }

            bytes = rawBytes;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string GetPortalAdminLogoDirectory()
    {
        var webRootPath = string.IsNullOrWhiteSpace(_hostEnvironment.WebRootPath)
            ? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot")
            : _hostEnvironment.WebRootPath;

        return Path.Combine(webRootPath, "uploads", "logos");
    }

    private string GetTenantUploadRootDirectory(string folderName)
    {
        var webRootPath = string.IsNullOrWhiteSpace(_hostEnvironment.WebRootPath)
            ? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot")
            : _hostEnvironment.WebRootPath;

        return Path.Combine(webRootPath, "uploads", folderName);
    }

    private bool TryReadRelativeFileUnderRoot(string rootDirectory, string childPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            var normalizedRoot = Path.GetFullPath(rootDirectory);
            var sanitizedChildPath = childPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var combinedPath = Path.GetFullPath(Path.Combine(normalizedRoot, sanitizedChildPath));

            if (!combinedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryReadFileBytes(combinedPath, out bytes);
        }
        catch
        {
            return false;
        }
    }

    private static DateTimeOffset ToMaldivesTime(DateTimeOffset utcDateTime)
    {
        return utcDateTime.ToOffset(MaldivesOffset);
    }
}
