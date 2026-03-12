using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Dashboard.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/dashboard")]
[Authorize]
public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetSummaryAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<ApiResponse<DashboardAnalyticsDto>>> GetAnalytics(
        [FromQuery] int topCustomers = 5,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(topCustomers, 1, 10);
        var result = await _dashboardService.GetAnalyticsAsync(safeLimit, cancellationToken);
        return OkResponse(result);
    }
}
