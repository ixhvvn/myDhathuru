using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/auth")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminAuthController : BaseApiController
{
    private readonly IPortalAdminAuthService _portalAdminAuthService;

    public PortalAdminAuthController(IPortalAdminAuthService portalAdminAuthService)
    {
        _portalAdminAuthService = portalAdminAuthService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _portalAdminAuthService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return OkResponse(result, "Portal admin login successful.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _portalAdminAuthService.RefreshTokenAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return OkResponse(result, "Portal admin token refreshed.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminAuthService.ForgotPasswordAsync(request, cancellationToken);
        return SuccessMessage("If an account exists, a reset link has been sent to email.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminAuthService.ResetPasswordAsync(request, cancellationToken);
        return SuccessMessage("Password has been reset successfully.");
    }

    [HttpGet("me")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(CancellationToken cancellationToken)
    {
        var result = await _portalAdminAuthService.GetCurrentProfileAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] PortalAdminChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminAuthService.ChangePasswordAsync(request, cancellationToken);
        return SuccessMessage("Password updated successfully.");
    }
}

