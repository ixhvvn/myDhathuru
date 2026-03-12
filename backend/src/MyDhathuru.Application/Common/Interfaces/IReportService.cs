using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IReportService
{
    Task<SalesSummaryReportDto> GetSalesSummaryAsync(ReportFilterQuery query, CancellationToken cancellationToken = default);
    Task<SalesTransactionsReportDto> GetSalesTransactionsAsync(ReportFilterQuery query, CancellationToken cancellationToken = default);
    Task<SalesByVesselReportDto> GetSalesByVesselAsync(ReportFilterQuery query, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportExcelAsync(ReportExportRequest request, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportPdfAsync(ReportExportRequest request, CancellationToken cancellationToken = default);
}
