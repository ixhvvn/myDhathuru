using MyDhathuru.Application.Auth.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IAuthService
{
    Task<SignupRequestSubmittedDto> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default);
}
