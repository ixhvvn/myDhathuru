using MyDhathuru.Application.Dashboard.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<DashboardAnalyticsDto> GetAnalyticsAsync(int topCustomersLimit = 5, CancellationToken cancellationToken = default);
}
