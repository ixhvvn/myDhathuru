using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPortalAdminAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(PortalAdminChangePasswordRequest request, CancellationToken cancellationToken = default);
}

