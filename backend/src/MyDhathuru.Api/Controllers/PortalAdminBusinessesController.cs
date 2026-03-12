using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/businesses")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminBusinessesController : BaseApiController
{
    private readonly IPortalAdminService _portalAdminService;

    public PortalAdminBusinessesController(IPortalAdminService portalAdminService)
    {
        _portalAdminService = portalAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminBusinessListItemDto>>>> GetList([FromQuery] PortalAdminBusinessListQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetBusinessesAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<ApiResponse<PortalAdminBusinessDetailDto>>> GetById(Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetBusinessByIdAsync(tenantId, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("{tenantId:guid}/disable")]
    public async Task<ActionResult<ApiResponse<object>>> Disable(Guid tenantId, [FromBody] PortalAdminSetBusinessStatusRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminService.DisableBusinessAsync(tenantId, request, cancellationToken);
        return SuccessMessage("Business account disabled.");
    }

    [HttpPost("{tenantId:guid}/enable")]
    public async Task<ActionResult<ApiResponse<object>>> Enable(Guid tenantId, CancellationToken cancellationToken)
    {
        await _portalAdminService.EnableBusinessAsync(tenantId, cancellationToken);
        return SuccessMessage("Business account enabled.");
    }

    [HttpPut("{tenantId:guid}/login-details")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLoginDetails(Guid tenantId, [FromBody] PortalAdminUpdateBusinessLoginRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminService.UpdateBusinessLoginDetailsAsync(tenantId, request, cancellationToken);
        return SuccessMessage("Business login details updated.");
    }

    [HttpPost("{tenantId:guid}/send-reset-link")]
    public async Task<ActionResult<ApiResponse<object>>> SendResetLink(Guid tenantId, [FromBody] PortalAdminSendResetLinkRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminService.SendBusinessPasswordResetLinkAsync(tenantId, request, cancellationToken);
        return SuccessMessage("Password reset link sent.");
    }

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminBusinessUserDto>>>> GetUsers([FromQuery] PortalAdminBusinessUsersQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetBusinessUsersAsync(query, cancellationToken);
        return OkResponse(result);
    }
}

