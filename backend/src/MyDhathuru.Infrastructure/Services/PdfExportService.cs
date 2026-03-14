using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Application.Statements.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MyDhathuru.Infrastructure.Services;

public class PdfExportService : IPdfExportService
{
    private static readonly TimeSpan MaldivesOffset = TimeSpan.FromHours(5);
    private const int MaxPortalAdminLogoBytes = 5 * 1024 * 1024;
    private const string DefaultAppLogoFileName = "logo.svg";
    private const string DefaultInvoiceLogoFileName = "logo-name.svg";
    private static readonly HttpClient LogoHttpClient = new() { Timeout = TimeSpan.FromSeconds(6) };

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
        var vesselPaymentTotal = model.Items.Sum(x => x.VesselPayment);
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
                                        brand.ConstantItem(60)
                                            .Height(60)
                                            .Border(1)
                                            .BorderColor(Border)
                                            .Background(Colors.White)
                                            .Padding(4)
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

                            row.ConstantItem(220)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
                                .Padding(10)
                                .Column(meta =>
                                {
                                    meta.Spacing(3);
                                    meta.Item().AlignRight().Text("DELIVERY NOTE").Bold().FontSize(16).FontColor(Ink);
                                    meta.Item().AlignRight().Text($"DN No: {Safe(model.DeliveryNoteNo)}").SemiBold();
                                    meta.Item().AlignRight().Text($"Date: {model.Date:yyyy-MM-dd}");
                                    meta.Item().AlignRight().Text($"Currency: {currency}");
                                    meta.Item().AlignRight().Text($"Status: {documentStatus}");
                                });
                        });

                        header.Item().LineHorizontal(1).LineColor(Border);
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
                                .Column(details =>
                                {
                                    details.Spacing(4);
                                    details.Item().Text("Customer Details").Bold().FontSize(10.4f).FontColor(Ink);
                                    details.Item().Text(model.CustomerName).Bold().FontSize(11.2f);
                                    details.Item().Text($"Vessel / Courier: {Safe(model.VesselName)}").FontColor(Muted);
                                    details.Item().Text($"PO Number: {Safe(model.PoNumber)}").FontColor(Muted);
                                    details.Item().Text($"Linked Invoice: {Safe(model.InvoiceNo)}").FontColor(Muted);
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
                                    var rowBackground = i % 2 == 0 ? "#FFFFFF" : "#FBFCFF";

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

                            row.ConstantItem(220)
                                .Border(1)
                                .BorderColor(Border)
                                .Background(Panel)
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

                                    summary.Item().LineHorizontal(1).LineColor(Border);

                                    summary.Item().Row(item =>
                                    {
                                        item.RelativeItem().Text("Balance").Bold().FontColor(Ink);
                                        item.ConstantItem(90).AlignRight().Text(Money(currency, balanceAmount)).Bold().FontColor(Ink);
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
                                text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                            });

                            row.ConstantItem(64).AlignRight().Text(text =>
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

    public byte[] BuildInvoicePdf(InvoiceDetailDto model, string companyName, string companyInfo, InvoiceBankDetailsDto bankDetails, string? logoUrl)
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
                                            text.Item().Text("Tax Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
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
                                        text.Item().Text("Tax Invoice").FontSize(10.6f).SemiBold().FontColor(Accent);
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                                    billTo.Item().Text($"Customer TIN: {Safe(model.CustomerTinNumber)}").FontColor(Muted);
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
                                    table.Cell().Element(LabelCell).Text("Tax Rate").SemiBold().FontColor(Muted);
                                    table.Cell().Element(ValueCell).Text($"{gstPercentText}%");
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
                            row.RelativeItem().Element(c => Metric(c, $"GST ({gstPercentText}%)", Money(currency, model.TaxAmount)));
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Staff Name").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Designation").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Work Site").Bold();
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
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Account Number").Bold();
                                header.Cell().Element(HeaderCell).AlignCenter().Text("Total Salary").Bold();
                            });

                            foreach (var entry in model.Entries)
                            {
                                table.Cell().Element(BodyCell).Text(entry.StaffCode);
                                table.Cell().Element(BodyCell).Text(entry.StaffName);
                                table.Cell().Element(BodyCell).Text(Safe(entry.Designation));
                                table.Cell().Element(BodyCell).Text(Safe(entry.WorkSite));
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
                                table.Cell().Element(BodyCell).Text(Safe(entry.AccountNumber));
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

    public byte[] BuildSalesSummaryReportPdf(SalesSummaryReportDto model, string companyName, string companyInfo, string? logoUrl)
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                            row.RelativeItem().Element(c => Metric(c, "Tax Posted", $"{Money("MVR", model.TotalTax.Mvr)} | {Money("USD", model.TotalTax.Usd)}", "Tax recognized across all invoices.", "#FFF8EE"));
                            row.RelativeItem().Element(c => Metric(c, "Avg Invoices / Day", averageInvoicesPerDay.ToString("N2"), "Average invoice volume per reported day.", "#F9F8FF"));
                            row.RelativeItem().Element(c => Metric(c, "Coverage Days", model.Rows.Count.ToString("N0"), "Daily rows present in the exported range.", "#F6FBFF"));
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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
                        header.Item().LineHorizontal(1).LineColor(Border);
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


    private static string BuildMetaLine(ReportMetaDto meta)
    {
        return
            $"Preset: {meta.DatePreset} | Range: {ToMaldivesTime(meta.RangeStartUtc):yyyy-MM-dd HH:mm} to {ToMaldivesTime(meta.RangeEndUtc):yyyy-MM-dd HH:mm} MVT | Customer: {meta.CustomerFilterLabel} | Generated: {ToMaldivesTime(meta.GeneratedAtUtc):yyyy-MM-dd HH:mm} MVT";
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
        var logoRootDirectory = GetTenantLogoRootDirectory();
        if (string.IsNullOrWhiteSpace(logoRootDirectory))
        {
            return false;
        }

        var normalized = pathOrUrl.Split('?', 2)[0].Split('#', 2)[0].Trim().Replace('\\', '/');
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativePath = normalized.TrimStart('/');
        const string prefix = "uploads/company-logos/";
        if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var childPath = relativePath[prefix.Length..];
        if (string.IsNullOrWhiteSpace(childPath))
        {
            return false;
        }

        return TryReadRelativeFileUnderRoot(logoRootDirectory, childPath, out bytes);
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

    private string GetTenantLogoRootDirectory()
    {
        var webRootPath = string.IsNullOrWhiteSpace(_hostEnvironment.WebRootPath)
            ? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot")
            : _hostEnvironment.WebRootPath;

        return Path.Combine(webRootPath, "uploads", "company-logos");
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
