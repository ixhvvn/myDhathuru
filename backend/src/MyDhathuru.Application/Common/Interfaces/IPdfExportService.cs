using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Application.Customers.Dtos;
using MyDhathuru.Application.Statements.Dtos;
using MyDhathuru.Application.Reports.Dtos;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPdfExportService
{
    byte[] BuildDeliveryNotePdf(DeliveryNoteDetailDto model, string companyName, string companyInfo);
    byte[] BuildInvoicePdf(InvoiceDetailDto model, string companyName, string companyInfo, InvoiceBankDetailsDto bankDetails, string? logoUrl);
    byte[] BuildStatementPdf(AccountStatementDto model, string companyName, string companyInfo);
    byte[] BuildSalarySlipPdf(SalarySlipDto model, string companyName, string companyInfo, string? logoUrl);
    byte[] BuildPayrollPeriodPdf(PayrollPeriodDetailDto model, string companyName, string companyInfo);
    byte[] BuildCustomersPdf(IReadOnlyList<CustomerDto> customers, string companyName, string companyInfo);
    byte[] BuildSalesSummaryReportPdf(SalesSummaryReportDto model, string companyName, string companyInfo);
    byte[] BuildSalesTransactionsReportPdf(SalesTransactionsReportDto model, string companyName, string companyInfo);
    byte[] BuildSalesByVesselReportPdf(SalesByVesselReportDto model, string companyName, string companyInfo);
    byte[] BuildPortalAdminInvoicePdf(PortalAdminBillingInvoiceDetailDto model, PortalAdminBillingSettingsDto settings);
}
