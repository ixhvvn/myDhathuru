using MyDhathuru.Domain.Entities;

namespace MyDhathuru.Infrastructure.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) GenerateAccessToken(User user, string role);
    string GenerateRefreshToken();
    string HashToken(string token);
}
