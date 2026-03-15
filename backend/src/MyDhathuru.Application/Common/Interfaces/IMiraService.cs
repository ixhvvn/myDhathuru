using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IMiraService
{
    Task<MiraReportPreviewDto> GetPreviewAsync(MiraReportQuery query, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportExcelAsync(MiraReportExportRequest request, CancellationToken cancellationToken = default);
    Task<ReportExportResultDto> ExportPdfAsync(MiraReportExportRequest request, CancellationToken cancellationToken = default);
}
