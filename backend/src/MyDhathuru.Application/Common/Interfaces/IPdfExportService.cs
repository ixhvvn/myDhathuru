using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Application.PurchaseOrders.Dtos;
using MyDhathuru.Application.Quotations.Dtos;
using MyDhathuru.Application.Statements.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Application.PaymentVouchers.Dtos;
using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Application.Expenses.Dtos;
using MyDhathuru.Application.StaffConduct.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPdfExportService
{
    byte[] BuildDeliveryNotePdf(DeliveryNoteDetailDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildInvoicePdf(InvoiceDetailDto model, string companyName, string companyInfo, InvoiceBankDetailsDto bankDetails, string? logoUrl, bool isTaxApplicable);
    byte[] BuildPurchaseOrderPdf(PurchaseOrderDetailDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable);
    byte[] BuildQuotationPdf(QuotationDetailDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable);
    byte[] BuildPaymentVoucherPdf(PaymentVoucherDetailDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildStatementPdf(AccountStatementDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildSalarySlipPdf(SalarySlipDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildPayrollPeriodPdf(PayrollPeriodDetailDto model, string companyName, string companyInfo);
    byte[] BuildCustomersPdf(IReadOnlyList<CustomerDto> customers, string companyName, string companyInfo, string? logoUrl, CustomerListQuery query);
    byte[] BuildSalesSummaryReportPdf(SalesSummaryReportDto model, string companyName, string companyInfo, string? logoUrl, bool isTaxApplicable);
    byte[] BuildSalesTransactionsReportPdf(SalesTransactionsReportDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildSalesByVesselReportPdf(SalesByVesselReportDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildPortalAdminInvoicePdf(PortalAdminBillingInvoiceDetailDto model, PortalAdminBillingSettingsDto settings);
    byte[] BuildExpenseLedgerPdf(IReadOnlyList<ExpenseLedgerRowDto> rows, ExpenseSummaryDto summary, string companyName, string companyInfo, string? logoUrl, ExpenseLedgerQuery query);
    byte[] BuildStaffConductFormPdf(StaffConductDetailDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildStaffConductSummaryPdf(IReadOnlyList<StaffConductListItemDto> rows, StaffConductSummaryDto summary, string companyName, string companyInfo, string? logoUrl, StaffConductListQuery query);
    byte[] BuildMiraInputTaxStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildMiraOutputTaxStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildBptIncomeStatementPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildBptNotesPdf(MiraReportPreviewDto model, string companyName, string companyInfo, string? logoUrl);
}
