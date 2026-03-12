using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Api.Controllers;

[Route("api/auth")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SignupRequestSubmittedDto>>> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.SignupAsync(request, cancellationToken);
        return OkResponse(result, "Signup request has been sent to portal admin.");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return OkResponse(result, "Login successful.");
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return OkResponse(result, "Token refreshed.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return SuccessMessage("If an account exists, a reset link has been sent to email.");
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return SuccessMessage("Password has been reset successfully.");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserProfileAsync(cancellationToken);
        return OkResponse(result);
    }
}
