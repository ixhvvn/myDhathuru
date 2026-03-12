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

    public byte[] BuildDeliveryNotePdf(DeliveryNoteDetailDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().Column(column =>
                    {
                        column.Item().Text(companyName).Bold().FontSize(18);
                        column.Item().Text(companyInfo).FontSize(9).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(8).Text("Delivery Note").Bold().FontSize(14);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"DN No: {model.DeliveryNoteNo}");
                            row.RelativeItem().AlignRight().Text($"Date: {model.Date:yyyy-MM-dd}");
                        });
                        if (!string.IsNullOrWhiteSpace(model.PoNumber))
                        {
                            column.Item().Text($"PO No: {model.PoNumber}");
                        }
                        column.Item().Text($"Customer: {model.CustomerName}");
                        column.Item().Text($"Currency: {model.Currency}");
                        if (!string.IsNullOrWhiteSpace(model.VesselName))
                        {
                            column.Item().Text($"Vessel: {model.VesselName}");
                        }

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(5);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Details").Bold();
                                header.Cell().AlignRight().Text("Qty").Bold();
                                header.Cell().AlignRight().Text("Rate").Bold();
                                header.Cell().AlignRight().Text("Total").Bold();
                            });

                            foreach (var item in model.Items)
                            {
                                table.Cell().Text(item.Details);
                                table.Cell().AlignRight().Text(item.Qty.ToString("N2"));
                                table.Cell().AlignRight().Text($"{model.Currency} {item.Rate:N2}");
                                table.Cell().AlignRight().Text($"{model.Currency} {item.Total:N2}");
                            }
                        });

                        column.Item().AlignRight().Text($"Total: {model.Currency} {model.TotalAmount:N2}").Bold();
                        if (!string.IsNullOrWhiteSpace(model.Notes))
                        {
                            column.Item().Text($"Notes: {model.Notes}");
                        }
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildInvoicePdf(InvoiceDetailDto model, string companyName, string companyInfo, InvoiceBankDetailsDto bankDetails, string? logoUrl)
    {
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
        var logoAsset = ResolveTenantInvoiceLogo(logoUrl);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f));

                    page.Header().Column(header =>
                    {
                        header.Spacing(6);
                        header.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(94)
                                            .Height(52)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Item().Text(companyName).Bold().FontSize(16);
                                            text.Item().Text("Tax Invoice").FontSize(10.2f).FontColor(Colors.Grey.Darken2);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Colors.Grey.Darken1);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Item().Text(companyName).Bold().FontSize(16);
                                        text.Item().Text("Tax Invoice").FontSize(10.2f).FontColor(Colors.Grey.Darken2);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Colors.Grey.Darken1);
                                    });
                                }
                            });

                            row.ConstantItem(230).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(meta =>
                            {
                                meta.Spacing(2);
                                meta.Item().AlignRight().Text("INVOICE").Bold().FontSize(18);
                                meta.Item().AlignRight().Text($"Invoice #: {model.InvoiceNo}");
                                meta.Item().AlignRight().Text($"Issued: {model.DateIssued:yyyy-MM-dd}");
                                meta.Item().AlignRight().Text($"Due: {model.DateDue:yyyy-MM-dd}");
                                meta.Item().AlignRight().Text($"Currency: {currency}");
                            });
                        });

                        header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(billTo =>
                            {
                                billTo.Spacing(2);
                                billTo.Item().Text("Bill To").Bold().FontSize(10.5f);
                                billTo.Item().Text(model.CustomerName).Bold();
                                billTo.Item().Text($"Customer Ref/TIN: {Safe(model.CustomerTinNumber)}");
                                billTo.Item().Text($"Delivery Note: {Safe(model.DeliveryNoteNo)}");
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(80);
                                    columns.RelativeColumn();
                                });

                                IContainer LabelCell(IContainer c) => c.PaddingVertical(1);
                                IContainer ValueCell(IContainer c) => c.PaddingVertical(1);

                                table.Cell().Element(LabelCell).Text("PO No.").SemiBold();
                                table.Cell().Element(ValueCell).Text(Safe(model.PoNumber));
                                table.Cell().Element(LabelCell).Text("Courier").SemiBold();
                                table.Cell().Element(ValueCell).Text(Safe(model.CourierName));
                                table.Cell().Element(LabelCell).Text("Tax Rate").SemiBold();
                                table.Cell().Element(ValueCell).Text($"{gstPercentText}%");
                                table.Cell().Element(LabelCell).Text("Payment Status").SemiBold();
                                table.Cell().Element(ValueCell).Text(model.PaymentStatus.ToString());
                            });
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

                            IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Background(Colors.Grey.Lighten4);
                            IContainer BodyCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                            IContainer SummaryLabel(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Background(Colors.Grey.Lighten5);
                            IContainer SummaryValue(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold();
                                header.Cell().Element(HeaderCell).Text("Description").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Qty").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold();
                            });

                            if (model.Items.Count == 0)
                            {
                                table.Cell().ColumnSpan(5).Element(BodyCell).AlignCenter().Text("No line items");
                            }
                            else
                            {
                                for (var i = 0; i < model.Items.Count; i++)
                                {
                                    var item = model.Items[i];
                                    table.Cell().Element(BodyCell).AlignCenter().Text((i + 1).ToString());
                                    table.Cell().Element(BodyCell).Text(item.Description);
                                    table.Cell().Element(BodyCell).AlignRight().Text(item.Qty.ToString("N2"));
                                    table.Cell().Element(BodyCell).AlignRight().Text($"{currency} {item.Rate:N2}");
                                    table.Cell().Element(BodyCell).AlignRight().Text($"{currency} {item.Total:N2}");
                                }
                            }

                            table.Cell().ColumnSpan(4).Element(SummaryLabel).AlignRight().Text("Subtotal").SemiBold();
                            table.Cell().Element(SummaryValue).AlignRight().Text($"{currency} {model.Subtotal:N2}");

                            table.Cell().ColumnSpan(4).Element(SummaryLabel).AlignRight().Text($"GST ({gstPercentText}%)").SemiBold();
                            table.Cell().Element(SummaryValue).AlignRight().Text($"{currency} {model.TaxAmount:N2}");

                            table.Cell().ColumnSpan(4).Element(SummaryLabel).AlignRight().Text("Total").Bold();
                            table.Cell().Element(SummaryValue).AlignRight().Text($"{currency} {model.GrandTotal:N2}").Bold();

                            table.Cell().ColumnSpan(4).Element(SummaryLabel).AlignRight().Text("Outstanding").Bold();
                            table.Cell().Element(SummaryValue).AlignRight().Text($"{currency} {outstandingAmount:N2}").Bold();
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(terms =>
                            {
                                terms.Spacing(3);
                                terms.Item().Text("Payment Terms").Bold().FontSize(10.3f);
                                terms.Item().Text($"Please settle this invoice within {dueDays} day(s) after receipt.");
                                if (!string.IsNullOrWhiteSpace(model.Notes))
                                {
                                    terms.Item().Text($"Notes: {model.Notes}");
                                }
                            });

                            row.ConstantItem(258).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(owner =>
                            {
                                owner.Spacing(3);
                                owner.Item().Text("Authorized Signatory").Bold().FontSize(10.3f);
                                owner.Item().Text($"Owner: {Safe(bankDetails.InvoiceOwnerName)}");
                                owner.Item().Text($"ID: {Safe(bankDetails.InvoiceOwnerIdCard)}");
                                owner.Item().PaddingTop(14).LineHorizontal(0.7f).LineColor(Colors.Grey.Lighten1);
                                owner.Item().Text("Signature").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                            });
                        });

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(payment =>
                        {
                            payment.Spacing(4);
                            payment.Item().Text("Payment Details").Bold().FontSize(10.3f);

                            payment.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(58);
                                    columns.ConstantColumn(58);
                                    columns.RelativeColumn(1.7f);
                                    columns.RelativeColumn(1.3f);
                                });

                                IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Background(Colors.Grey.Lighten4);
                                IContainer BodyCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).AlignCenter().Text("Bank").Bold();
                                    header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold();
                                    header.Cell().Element(HeaderCell).Text("Account Name").Bold();
                                    header.Cell().Element(HeaderCell).Text("Account Number").Bold();
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

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(receipt =>
                        {
                            receipt.Spacing(3);
                            receipt.Item().Text("Payment Receipt (Optional)").Bold().FontSize(10.3f);
                            receipt.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.1f);
                                    columns.RelativeColumn();
                                });

                                IContainer Cell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                                table.Cell().Element(Cell).Text("Payment Method");
                                table.Cell().Element(Cell).Text(string.Empty);
                                table.Cell().Element(Cell).Text("Name");
                                table.Cell().Element(Cell).Text(string.Empty);
                                table.Cell().Element(Cell).Text("Date / Signature");
                                table.Cell().Element(Cell).Text(string.Empty);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2));
                        text.Span("Generated by myDhathuru");
                        text.Span(" • ");
                        text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildStatementPdf(AccountStatementDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().Column(column =>
                    {
                        column.Item().Text(companyName).Bold().FontSize(18);
                        column.Item().Text(companyInfo).FontSize(9).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(8).Text("Statement Of Account").Bold().FontSize(14);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        column.Item().Text($"Customer: {model.CustomerName}");
                        column.Item().Text($"Statement No: {model.StatementNo}");
                        column.Item().Text($"Year: {model.Year}");

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("#").Bold();
                                header.Cell().Text("Date").Bold();
                                header.Cell().Text("Description").Bold();
                                header.Cell().Text("Reference").Bold();
                                header.Cell().Text("Currency").Bold();
                                header.Cell().AlignRight().Text("Amount").Bold();
                                header.Cell().AlignRight().Text("Payments").Bold();
                                header.Cell().Text("Received On").Bold();
                                header.Cell().AlignRight().Text("Balance").Bold();
                            });

                            foreach (var row in model.Rows)
                            {
                                table.Cell().Text(row.Index.ToString());
                                table.Cell().Text(row.Date?.ToString("yyyy-MM-dd") ?? "-");
                                table.Cell().Text(row.Description);
                                table.Cell().Text(row.Reference ?? "-");
                                table.Cell().Text(row.Currency);
                                table.Cell().AlignRight().Text(row.Amount.ToString("N2"));
                                table.Cell().AlignRight().Text(row.Payments.ToString("N2"));
                                table.Cell().Text(row.ReceivedOn?.ToString("yyyy-MM-dd") ?? "-");
                                table.Cell().AlignRight().Text(row.Balance.ToString("N2"));
                            }
                        });

                        column.Item().AlignRight().Column(totals =>
                        {
                            totals.Item().Text($"Opening Balance: MVR {model.OpeningBalance.Mvr:N2} | USD {model.OpeningBalance.Usd:N2}");
                            totals.Item().Text($"Total Invoiced: MVR {model.TotalInvoiced.Mvr:N2} | USD {model.TotalInvoiced.Usd:N2}");
                            totals.Item().Text($"Total Received: MVR {model.TotalReceived.Mvr:N2} | USD {model.TotalReceived.Usd:N2}");
                            totals.Item().Text($"Pending: MVR {model.TotalPending.Mvr:N2} | USD {model.TotalPending.Usd:N2}").Bold();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalarySlipPdf(SalarySlipDto model, string companyName, string companyInfo, string? logoUrl)
    {
        static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        static string Money(decimal value) => $"MVR {Math.Round(value, 2, MidpointRounding.AwayFromZero):N2}";

        var periodLabel = $"{model.PeriodStart:dd/MM/yyyy} to {model.PeriodEnd:dd/MM/yyyy}";
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
            ("Risk Allowance", model.OtherAllowance),
            ("Phone Allowance", model.PhoneAllowance),
            ("Food Allowance", model.FoodAllowance),
            ("Overtime Pay", model.OvertimePay)
        };

        var deductionRows = new (string Label, string Basis, decimal Amount)[]
        {
            ("Absent Deduction", model.AbsentDays > 0 ? $"{model.AbsentDays:N0} x {model.RatePerDay:N2}" : "-", model.AbsentDeduction),
            ("Food Allowance (Cash)", model.FoodAllowanceDays > 0 ? $"{model.FoodAllowanceDays:N0} x {model.FoodAllowanceRate:N2}" : "-", foodAllowanceCashDeduction),
            ("Pension Contribution", "-", model.PensionDeduction),
            ("Salary Advance", "-", model.SalaryAdvanceDeduction)
        };

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9.6f));

                    page.Header().Column(header =>
                    {
                        header.Spacing(6);
                        header.Item().Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(8);
                                        brand.ConstantItem(94)
                                            .Height(52)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Item().Text(companyName).Bold().FontSize(16);
                                            text.Item().Text("Salary Slip").FontSize(10.2f).FontColor(Colors.Grey.Darken2);
                                            text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Colors.Grey.Darken1);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Item().Text(companyName).Bold().FontSize(16);
                                        text.Item().Text("Salary Slip").FontSize(10.2f).FontColor(Colors.Grey.Darken2);
                                        text.Item().Text(companyInfo).FontSize(8.6f).FontColor(Colors.Grey.Darken1);
                                    });
                                }
                            });

                            row.ConstantItem(235).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(meta =>
                            {
                                meta.Spacing(2);
                                meta.Item().AlignRight().Text("PAY SLIP").Bold().FontSize(17);
                                meta.Item().AlignRight().Text($"Slip No: {model.SlipNo}");
                                meta.Item().AlignRight().Text($"Period: {periodLabel}");
                                meta.Item().AlignRight().Text($"Staff Code: {model.StaffCode}");
                            });
                        });

                        header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(108);
                                    columns.RelativeColumn();
                                });

                                IContainer LabelCell(IContainer c) => c.PaddingVertical(1);
                                IContainer ValueCell(IContainer c) => c.PaddingVertical(1);

                                table.Cell().Element(LabelCell).Text("Staff Name").SemiBold();
                                table.Cell().Element(ValueCell).Text(model.StaffName);
                                table.Cell().Element(LabelCell).Text("Designation").SemiBold();
                                table.Cell().Element(ValueCell).Text(Safe(model.Designation));
                                table.Cell().Element(LabelCell).Text("Work Site").SemiBold();
                                table.Cell().Element(ValueCell).Text(Safe(model.WorkSite));
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(108);
                                    columns.RelativeColumn();
                                });

                                IContainer LabelCell(IContainer c) => c.PaddingVertical(1);
                                IContainer ValueCell(IContainer c) => c.PaddingVertical(1);

                                table.Cell().Element(LabelCell).Text("Attended Days").SemiBold();
                                table.Cell().Element(ValueCell).Text(model.AttendedDays.ToString("N0"));
                                table.Cell().Element(LabelCell).Text("Absent Days").SemiBold();
                                table.Cell().Element(ValueCell).Text(model.AbsentDays.ToString("N0"));
                                table.Cell().Element(LabelCell).Text("Food Allowance Days").SemiBold();
                                table.Cell().Element(ValueCell).Text(model.FoodAllowanceDays.ToString("N0"));
                            });
                        });

                        column.Item().Row(row =>
                        {
                            row.Spacing(8);

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(earnings =>
                            {
                                earnings.Spacing(4);
                                earnings.Item().Text("Earnings").Bold().FontSize(10.4f);

                                earnings.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.ConstantColumn(58);
                                        columns.ConstantColumn(96);
                                    });

                                    IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Background(Colors.Grey.Lighten4);
                                    IContainer BodyCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCell).Text("Description").Bold();
                                        header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold();
                                        header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold();
                                    });

                                    foreach (var item in earningRows)
                                    {
                                        table.Cell().Element(BodyCell).Text(item.Label);
                                        table.Cell().Element(BodyCell).AlignCenter().Text("MVR");
                                        table.Cell().Element(BodyCell).AlignRight().Text(item.Amount.ToString("N2"));
                                    }

                                    table.Cell().Element(HeaderCell).Text("Total Salary").Bold();
                                    table.Cell().Element(HeaderCell).AlignCenter().Text("MVR").Bold();
                                    table.Cell().Element(HeaderCell).AlignRight().Text(model.TotalSalary.ToString("N2")).Bold();
                                });
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(deductions =>
                            {
                                deductions.Spacing(4);
                                deductions.Item().Text("Deductions").Bold().FontSize(10.4f);

                                deductions.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1.8f);
                                        columns.RelativeColumn(1.2f);
                                        columns.ConstantColumn(58);
                                        columns.ConstantColumn(96);
                                    });

                                    IContainer HeaderCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Background(Colors.Grey.Lighten4);
                                    IContainer BodyCell(IContainer c) => c.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(4);

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(HeaderCell).Text("Description").Bold();
                                        header.Cell().Element(HeaderCell).Text("Basis").Bold();
                                        header.Cell().Element(HeaderCell).AlignCenter().Text("CCY").Bold();
                                        header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold();
                                    });

                                    foreach (var item in deductionRows)
                                    {
                                        table.Cell().Element(BodyCell).Text(item.Label);
                                        table.Cell().Element(BodyCell).Text(item.Basis);
                                        table.Cell().Element(BodyCell).AlignCenter().Text("MVR");
                                        table.Cell().Element(BodyCell).AlignRight().Text(item.Amount.ToString("N2"));
                                    }

                                    table.Cell().ColumnSpan(2).Element(HeaderCell).Text("Total Deduction").Bold();
                                    table.Cell().Element(HeaderCell).AlignCenter().Text("MVR").Bold();
                                    table.Cell().Element(HeaderCell).AlignRight().Text(totalDeduction.ToString("N2")).Bold();
                                });
                            });
                        });

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            IContainer MetricLabel(IContainer c) => c.PaddingBottom(2);

                            table.Cell().Column(cell =>
                            {
                                cell.Item().Element(MetricLabel).Text("Total Salary").SemiBold().FontColor(Colors.Grey.Darken2);
                                cell.Item().Text(Money(model.TotalSalary)).Bold().FontSize(11);
                            });

                            table.Cell().Column(cell =>
                            {
                                cell.Item().Element(MetricLabel).Text("Total Deduction").SemiBold().FontColor(Colors.Grey.Darken2);
                                cell.Item().Text(Money(totalDeduction)).Bold().FontSize(11);
                            });

                            table.Cell().Column(cell =>
                            {
                                cell.Item().Element(MetricLabel).Text("Net Payable").SemiBold().FontColor(Colors.Grey.Darken2);
                                cell.Item().Text(Money(model.TotalPayable)).Bold().FontSize(12);
                            });
                        });

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(bank =>
                        {
                            bank.Spacing(3);
                            bank.Item().Text("Bank Transfer Details").Bold().FontSize(10.4f);
                            bank.Item().Text($"Bank: {Safe(model.BankName)}");
                            bank.Item().Text($"Account Name: {Safe(payoutAccountName)}");
                            bank.Item().Text($"Account Number: {Safe(model.AccountNumber)}");
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2));
                        text.Span("This is a system-generated salary slip.");
                        text.Span(" • ");
                        text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildCustomersPdf(IReadOnlyList<CustomerDto> customers, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Header().Column(column =>
                    {
                        column.Item().Text(companyName).Bold().FontSize(18);
                        column.Item().Text(companyInfo).FontSize(9).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(8).Text("Customer List").Bold().FontSize(14);
                        column.Item().Text($"Total Customers: {customers.Count}");
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.7f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(2.1f);
                            columns.RelativeColumn(2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Customer Name").Bold();
                            header.Cell().Text("TIN").Bold();
                            header.Cell().Text("Phone").Bold();
                            header.Cell().Text("Email").Bold();
                            header.Cell().Text("References").Bold();
                        });

                        foreach (var customer in customers)
                        {
                            table.Cell().Text(customer.Name);
                            table.Cell().Text(customer.TinNumber ?? "-");
                            table.Cell().Text(customer.Phone ?? "-");
                            table.Cell().Text(customer.Email ?? "-");
                            table.Cell().Text(customer.References.Any() ? string.Join(", ", customer.References) : "-");
                        }
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPayrollPeriodPdf(PayrollPeriodDetailDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(16);
                    page.DefaultTextStyle(x => x.FontSize(8.5f));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        column.Item().Text(companyName).Bold().FontSize(15);
                        column.Item().Text(companyInfo).FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(3).Text($"Payroll Detail - {model.Year}-{model.Month:00}").Bold().FontSize(11.5f);
                        column.Item().Text(
                            $"Period: {model.StartDate:yyyy-MM-dd} to {model.EndDate:yyyy-MM-dd} | Days: {model.PeriodDays} | Entries: {model.Entries.Count} | Total Net Payable: MVR {model.TotalNetPayable:N2}")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2f);    // staff
                            columns.RelativeColumn(0.65f); // attended
                            columns.RelativeColumn(0.65f); // food days
                            columns.RelativeColumn(0.85f); // ot pay
                            columns.RelativeColumn(0.95f); // salary advance
                            columns.RelativeColumn(0.85f); // pension
                            columns.RelativeColumn(0.95f); // total pay
                            columns.RelativeColumn(0.95f); // net payable
                            columns.RelativeColumn(0.75f); // bank
                            columns.RelativeColumn(1.05f); // account name
                            columns.RelativeColumn(1.05f); // account number
                            columns.RelativeColumn(0.95f); // designation
                            columns.RelativeColumn(0.95f); // work site
                        });

                        IContainer HeaderCell(IContainer c) => c.Border(1).Background(Colors.Grey.Lighten3).Padding(3);
                        IContainer BodyCell(IContainer c) => c.Border(1).Padding(3);

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Staff").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Attended").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Food Days").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("OT Pay").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Salary Advance").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Pension").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Total Pay").Bold();
                            header.Cell().Element(HeaderCell).AlignRight().Text("Net Payable").Bold();
                            header.Cell().Element(HeaderCell).Text("Bank").Bold();
                            header.Cell().Element(HeaderCell).Text("Account Name").Bold();
                            header.Cell().Element(HeaderCell).Text("Account Number").Bold();
                            header.Cell().Element(HeaderCell).Text("Designation").Bold();
                            header.Cell().Element(HeaderCell).Text("Work Site").Bold();
                        });

                        foreach (var entry in model.Entries)
                        {
                            table.Cell().Element(BodyCell).Text($"{entry.StaffCode} - {entry.StaffName}");
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.AttendedDays.ToString("N0"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.FoodAllowanceDays.ToString("N0"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.OvertimePay.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.SalaryAdvanceDeduction.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.PensionDeduction.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.TotalPay.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(entry.NetPayable.ToString("N2"));
                            table.Cell().Element(BodyCell).Text(entry.BankName ?? "-");
                            table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.AccountName) ? "-" : entry.AccountName);
                            table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.AccountNumber) ? "-" : entry.AccountNumber);
                            table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Designation) ? "-" : entry.Designation);
                            table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.WorkSite) ? "-" : entry.WorkSite);
                        }

                        table.Cell().ColumnSpan(7).Element(BodyCell).AlignRight().Text("TOTAL").Bold();
                        table.Cell().Element(BodyCell).AlignRight().Text(model.TotalNetPayable.ToString("N2")).Bold();
                        table.Cell().Element(BodyCell).Text(string.Empty);
                        table.Cell().Element(BodyCell).Text(string.Empty);
                        table.Cell().Element(BodyCell).Text(string.Empty);
                        table.Cell().Element(BodyCell).Text(string.Empty);
                        table.Cell().Element(BodyCell).Text(string.Empty);
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesSummaryReportPdf(SalesSummaryReportDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        column.Item().Text(companyName).Bold().FontSize(16);
                        column.Item().Text(companyInfo).FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(4).Text("Sales Summary Report").Bold().FontSize(12);
                        column.Item().Text(BuildMetaLine(model.Meta)).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(8);

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            void Metric(string label, string value)
                            {
                                table.Cell().Border(1).Padding(4).Text(label).Bold().FontSize(8);
                                table.Cell().Border(1).Padding(4).Text(value).AlignRight();
                            }

                            Metric("Total Invoices", model.TotalInvoices.ToString("N0"));
                            Metric("Total Sales (MVR)", model.TotalSales.Mvr.ToString("N2"));
                            Metric("Total Sales (USD)", model.TotalSales.Usd.ToString("N2"));
                            Metric("Total Received (MVR)", model.TotalReceived.Mvr.ToString("N2"));
                            Metric("Total Received (USD)", model.TotalReceived.Usd.ToString("N2"));
                            Metric("Total Customers", model.TotalCustomers.ToString("N0"));
                            Metric("Total Pending (MVR)", model.TotalPending.Mvr.ToString("N2"));
                            Metric("Total Pending (USD)", model.TotalPending.Usd.ToString("N2"));
                            Metric("Total Tax (MVR)", model.TotalTax.Mvr.ToString("N2"));
                            Metric("Total Tax (USD)", model.TotalTax.Usd.ToString("N2"));
                        });

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).Padding(4).Background(Colors.Grey.Lighten3);
                            IContainer BodyCell(IContainer c) => c.Border(1).Padding(4);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Date").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Invoices").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Sales MVR").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Sales USD").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Received MVR").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Received USD").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Pending MVR").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Pending USD").Bold();
                            });

                            foreach (var row in model.Rows)
                            {
                                table.Cell().Element(BodyCell).Text(row.Date.ToString("yyyy-MM-dd"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.InvoiceCount.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.SalesMvr.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.SalesUsd.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.ReceivedMvr.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.ReceivedUsd.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.PendingMvr.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.PendingUsd.ToString("N2"));
                            }

                            table.Cell().Element(BodyCell).Text("TOTAL").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalInvoices.ToString("N0")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalSales.Mvr.ToString("N2")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalSales.Usd.ToString("N2")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalReceived.Mvr.ToString("N2")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalReceived.Usd.ToString("N2")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalPending.Mvr.ToString("N2")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalPending.Usd.ToString("N2")).Bold();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesTransactionsReportPdf(SalesTransactionsReportDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(16);
                    page.DefaultTextStyle(x => x.FontSize(8.2f));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        column.Item().Text(companyName).Bold().FontSize(15);
                        column.Item().Text(companyInfo).FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(3).Text("Sales Transactions Report").Bold().FontSize(11.5f);
                        column.Item().Text(BuildMetaLine(model.Meta)).FontSize(7.6f).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(6);
                        column.Item().Text(
                            $"Transactions: {model.TotalTransactions:N0} | Sales MVR: {model.TotalSales.Mvr:N2} | Sales USD: {model.TotalSales.Usd:N2} | Pending MVR: {model.TotalPending.Mvr:N2} | Pending USD: {model.TotalPending.Usd:N2}");

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(2.1f);
                                columns.RelativeColumn(0.8f);
                                columns.RelativeColumn(1.05f);
                                columns.RelativeColumn(1.05f);
                                columns.RelativeColumn(1.05f);
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(1.05f);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).Padding(3).Background(Colors.Grey.Lighten3);
                            IContainer BodyCell(IContainer c) => c.Border(1).Padding(3);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Invoice No").Bold();
                                header.Cell().Element(HeaderCell).Text("Date Issued").Bold();
                                header.Cell().Element(HeaderCell).Text("Customer").Bold();
                                header.Cell().Element(HeaderCell).Text("Vessel").Bold();
                                header.Cell().Element(HeaderCell).Text("Description").Bold();
                                header.Cell().Element(HeaderCell).Text("CCY").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold();
                                header.Cell().Element(HeaderCell).Text("Status").Bold();
                                header.Cell().Element(HeaderCell).Text("Method").Bold();
                                header.Cell().Element(HeaderCell).Text("Received On").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Balance").Bold();
                            });

                            foreach (var row in model.Rows)
                            {
                                table.Cell().Element(BodyCell).Text(row.InvoiceNo);
                                table.Cell().Element(BodyCell).Text(row.DateIssued.ToString("yyyy-MM-dd"));
                                table.Cell().Element(BodyCell).Text(row.Customer);
                                table.Cell().Element(BodyCell).Text(row.Vessel);
                                table.Cell().Element(BodyCell).Text(row.Description);
                                table.Cell().Element(BodyCell).Text(row.Currency);
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Amount.ToString("N2"));
                                table.Cell().Element(BodyCell).Text(row.PaymentStatus);
                                table.Cell().Element(BodyCell).Text(row.PaymentMethod);
                                table.Cell().Element(BodyCell).Text(row.ReceivedOn?.ToString("yyyy-MM-dd") ?? "-");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Balance.ToString("N2"));
                            }

                            table.Cell().Element(BodyCell).Text("TOTAL").Bold();
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{model.TotalSales.Mvr:N2}/{model.TotalSales.Usd:N2}").Bold();
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).Text(string.Empty);
                            table.Cell().Element(BodyCell).AlignRight().Text($"{model.TotalPending.Mvr:N2}/{model.TotalPending.Usd:N2}").Bold();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildSalesByVesselReportPdf(SalesByVesselReportDto model, string companyName, string companyInfo)
    {
        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Spacing(2);
                        column.Item().Text(companyName).Bold().FontSize(16);
                        column.Item().Text(companyInfo).FontSize(8).FontColor(Colors.Grey.Darken2);
                        column.Item().PaddingTop(4).Text("Sales By Vessel Report").Bold().FontSize(12);
                        column.Item().Text(BuildMetaLine(model.Meta)).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(8);
                        column.Item().Text(
                            $"Transactions: {model.TotalTransactions:N0} | Sales MVR: {model.TotalSales.Mvr:N2} | Sales USD: {model.TotalSales.Usd:N2} | Pending MVR: {model.TotalPending.Mvr:N2} | Pending USD: {model.TotalPending.Usd:N2}");

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2f);
                                columns.RelativeColumn(0.9f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.2f);
                            });

                            IContainer HeaderCell(IContainer c) => c.Border(1).Padding(4).Background(Colors.Grey.Lighten3);
                            IContainer BodyCell(IContainer c) => c.Border(1).Padding(4);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Vessel").Bold();
                                header.Cell().Element(HeaderCell).Text("CCY").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Transactions").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total Sales").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total Received").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Pending").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("% CCY Sales").Bold();
                            });

                            foreach (var row in model.Rows)
                            {
                                table.Cell().Element(BodyCell).Text(row.Vessel);
                                table.Cell().Element(BodyCell).Text(row.Currency);
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TransactionCount.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalSales.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalReceived.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.PendingAmount.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text($"{row.PercentageOfCurrencySales:N2}%");
                            }

                            table.Cell().Element(BodyCell).Text("TOTAL").Bold();
                            table.Cell().Element(BodyCell).Text("-").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text(model.TotalTransactions.ToString("N0")).Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text($"{model.TotalSales.Mvr:N2}/{model.TotalSales.Usd:N2}").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text($"{model.TotalReceived.Mvr:N2}/{model.TotalReceived.Usd:N2}").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text($"{model.TotalPending.Mvr:N2}/{model.TotalPending.Usd:N2}").Bold();
                            table.Cell().Element(BodyCell).AlignRight().Text("-").Bold();
                        });
                    });
                });
            })
            .GeneratePdf();
    }

    public byte[] BuildPortalAdminInvoicePdf(PortalAdminBillingInvoiceDetailDto model, PortalAdminBillingSettingsDto settings)
    {
        static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        var monthLabel = model.BillingMonth.ToString("MMMM yyyy");
        var currency = string.IsNullOrWhiteSpace(model.Currency) ? "MVR" : model.Currency.Trim().ToUpperInvariant();
        var logoAsset = ResolvePortalAdminLogo(settings.LogoUrl);

        return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9.8f));

                    page.Header().Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Element(left =>
                            {
                                if (logoAsset?.HasValue == true)
                                {
                                    left.Row(brand =>
                                    {
                                        brand.Spacing(10);
                                        brand.ConstantItem(106)
                                            .Height(54)
                                            .Border(1)
                                            .BorderColor(Colors.Grey.Lighten2)
                                            .Padding(4)
                                            .AlignCenter()
                                            .AlignMiddle()
                                            .Element(container => RenderLogo(container, logoAsset));
                                        brand.RelativeItem().Column(text =>
                                        {
                                            text.Item().Text("myDhathuru Platform").Bold().FontSize(18);
                                            text.Item().Text("Platform Billing Invoice").FontSize(10).FontColor(Colors.Grey.Darken2);
                                        });
                                    });
                                }
                                else
                                {
                                    left.Column(text =>
                                    {
                                        text.Item().Text("myDhathuru Platform").Bold().FontSize(18);
                                        text.Item().Text("Platform Billing Invoice").FontSize(10).FontColor(Colors.Grey.Darken2);
                                    });
                                }
                            });
                            row.ConstantItem(230).Column(right =>
                            {
                                right.Item().AlignRight().Text("INVOICE").Bold().FontSize(20);
                                right.Item().AlignRight().Text($"Invoice #: {model.InvoiceNumber}");
                                right.Item().AlignRight().Text($"Invoice Date: {model.InvoiceDate:yyyy-MM-dd}");
                                right.Item().AlignRight().Text($"Billing Month: {monthLabel}");
                                right.Item().AlignRight().Text($"Due Date: {model.DueDate:yyyy-MM-dd}");
                            });
                        });
                    });

                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(billTo =>
                        {
                            billTo.Spacing(2);
                            billTo.Item().Text("Bill To").Bold().FontSize(10.6f);
                            billTo.Item().Text(model.CompanyName).Bold();
                            billTo.Item().Text($"Email: {Safe(model.CompanyEmail)}");
                            billTo.Item().Text($"Phone: {Safe(model.CompanyPhone)}");
                            billTo.Item().Text($"TIN: {Safe(model.CompanyTinNumber)}");
                            billTo.Item().Text($"Registration: {Safe(model.CompanyRegistrationNumber)}");
                            billTo.Item().Text($"Primary Admin: {Safe(model.CompanyAdminName)} ({Safe(model.CompanyAdminEmail)})");
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

                            IContainer HeaderCell(IContainer c) => c.Border(1).Padding(5).Background(Colors.Grey.Lighten3);
                            IContainer BodyCell(IContainer c) => c.Border(1).Padding(5);

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text("#").Bold();
                                header.Cell().Element(HeaderCell).Text("Description").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Quantity").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Rate").Bold();
                                header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold();
                            });

                            for (var i = 0; i < model.LineItems.Count; i++)
                            {
                                var line = model.LineItems[i];
                                table.Cell().Element(BodyCell).AlignCenter().Text((i + 1).ToString());
                                table.Cell().Element(BodyCell).Text(line.Description);
                                table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("N2"));
                                table.Cell().Element(BodyCell).AlignRight().Text($"{currency} {line.Rate:N2}");
                                table.Cell().Element(BodyCell).AlignRight().Text($"{currency} {line.Amount:N2}");
                            }
                        });

                        column.Item().AlignRight().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(160);
                                columns.ConstantColumn(150);
                            });

                            IContainer TotalsLabel(IContainer c) => c.Border(1).Padding(5).Background(Colors.Grey.Lighten4);
                            IContainer TotalsValue(IContainer c) => c.Border(1).Padding(5);

                            table.Cell().Element(TotalsLabel).Text("Subtotal").Bold();
                            table.Cell().Element(TotalsValue).AlignRight().Text($"{currency} {model.Subtotal:N2}");
                            table.Cell().Element(TotalsLabel).Text("Total").Bold().FontSize(11);
                            table.Cell().Element(TotalsValue).AlignRight().Text($"{currency} {model.Total:N2}").Bold().FontSize(11);
                        });

                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(payment =>
                        {
                            payment.Spacing(2);
                            payment.Item().Text("Payment Details").Bold().FontSize(10.6f);
                            payment.Item().Text($"Account Name: {Safe(settings.AccountName)}");
                            payment.Item().Text($"Account Number: {Safe(settings.AccountNumber)}");
                            payment.Item().Text($"Bank: {Safe(settings.BankName)}");
                            payment.Item().Text($"Branch: {Safe(settings.Branch)}");
                            payment.Item().Text($"Instructions: {Safe(settings.PaymentInstructions)}");
                        });

                        if (!string.IsNullOrWhiteSpace(model.Notes))
                        {
                            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Text($"Notes: {model.Notes}");
                        }

                        if (!string.IsNullOrWhiteSpace(settings.InvoiceTerms))
                        {
                            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Text($"Terms: {settings.InvoiceTerms}");
                        }
                    });

                    page.Footer().Column(column =>
                    {
                        column.Spacing(2);
                        if (!string.IsNullOrWhiteSpace(settings.InvoiceFooterNote))
                        {
                            column.Item().AlignCenter().Text(settings.InvoiceFooterNote).FontSize(8.3f).FontColor(Colors.Grey.Darken1);
                        }

                        column.Item().AlignCenter().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken2));
                            text.Span("Generated by myDhathuru Portal Admin");
                            text.Span(" • ");
                            text.Span($"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
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
            fallbackLogoFileName: DefaultInvoiceLogoFileName,
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
