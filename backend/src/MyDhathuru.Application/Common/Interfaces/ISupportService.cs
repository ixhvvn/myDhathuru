using MyDhathuru.Application.Support.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface ISupportService
{
    Task ReportBugAsync(
        ReportBugRequest request,
        BugReportAttachment? attachment = null,
        CancellationToken cancellationToken = default);
}
