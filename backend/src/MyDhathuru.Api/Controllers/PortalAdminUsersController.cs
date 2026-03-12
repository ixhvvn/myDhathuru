using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/users")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminUsersController : BaseApiController
{
    private readonly IPortalAdminService _portalAdminService;

    public PortalAdminUsersController(IPortalAdminService portalAdminService)
    {
        _portalAdminService = portalAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminBusinessUserDto>>>> GetList([FromQuery] PortalAdminBusinessUsersQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetBusinessUsersAsync(query, cancellationToken);
        return OkResponse(result);
    }
}

