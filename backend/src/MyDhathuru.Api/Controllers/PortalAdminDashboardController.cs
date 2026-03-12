using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/dashboard")]
[Authorize(Policy = "SuperAdminOnly")]
public class PortalAdminDashboardController : BaseApiController
{
    private readonly IPortalAdminService _portalAdminService;

    public PortalAdminDashboardController(IPortalAdminService portalAdminService)
    {
        _portalAdminService = portalAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PortalAdminDashboardDto>>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetDashboardAsync(cancellationToken);
        return OkResponse(result);
    }
}

