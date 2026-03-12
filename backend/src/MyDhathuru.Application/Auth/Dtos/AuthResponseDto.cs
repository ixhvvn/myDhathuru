namespace MyDhathuru.Application.Auth.Dtos;

public class AuthResponseDto
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public DateTimeOffset AccessTokenExpiresAt { get; set; }
    public UserProfileDto User { get; set; } = null!;
}
