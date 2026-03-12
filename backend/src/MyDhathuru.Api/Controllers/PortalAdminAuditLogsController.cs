using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/audit-logs")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminAuditLogsController : BaseApiController
{
    private readonly IPortalAdminService _portalAdminService;

    public PortalAdminAuditLogsController(IPortalAdminService portalAdminService)
    {
        _portalAdminService = portalAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminAuditLogDto>>>> GetList([FromQuery] PortalAdminAuditLogQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetAuditLogsAsync(query, cancellationToken);
        return OkResponse(result);
    }
}

