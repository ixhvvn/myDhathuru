using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/settings")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminSettingsController : BaseApiController
{
    private readonly IPortalAdminAuthService _portalAdminAuthService;

    public PortalAdminSettingsController(IPortalAdminAuthService portalAdminAuthService)
    {
        _portalAdminAuthService = portalAdminAuthService;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile(CancellationToken cancellationToken)
    {
        var result = await _portalAdminAuthService.GetCurrentProfileAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] PortalAdminChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminAuthService.ChangePasswordAsync(request, cancellationToken);
        return SuccessMessage("Password updated.");
    }
}

