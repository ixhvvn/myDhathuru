using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.PortalAdmin.Dtos;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Configuration;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;

namespace MyDhathuru.Infrastructure.Services;

public class PortalAdminAuthService : IPortalAdminAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly JwtOptions _jwtOptions;

    public PortalAdminAuthService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.Email == email && x.IsActive && x.Role.Name == UserRoleName.SuperAdmin,
                cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedException("Invalid portal admin credentials.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenGenerator.HashToken(request.RefreshToken);
        var existing = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.Role)
            .Include(x => x.User)
            .ThenInclude(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (!existing.IsActive || existing.User.Role.Name != UserRoleName.SuperAdmin)
        {
            throw new UnauthorizedException("Refresh token expired or revoked.");
        }

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.RevokedByIp = ipAddress;

        var user = existing.User;
        var (accessToken, accessExpiresAt) = _tokenGenerator.GenerateAccessToken(user, user.Role.Name);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();
        var refreshHash = _tokenGenerator.HashToken(refreshToken);

        existing.ReplacedByTokenHash = refreshHash;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedByIp = ipAddress
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name,
                CompanyName = user.Tenant.CompanyName
            }
        };
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive && x.Role.Name == UserRoleName.SuperAdmin, cancellationToken);
        if (user is null)
        {
            return;
        }

        var token = _tokenGenerator.GenerateRefreshToken();
        var tokenHash = _tokenGenerator.HashToken(token);

        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var active in activeTokens)
        {
            active.UsedAt = DateTimeOffset.UtcNow;
        }

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _notificationService.SendPasswordResetAsync(email, token, isPortalAdmin: true, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive && x.Role.Name == UserRoleName.SuperAdmin, cancellationToken)
            ?? throw new AppException("Invalid reset request.");

        var tokenHash = _tokenGenerator.HashToken(request.Token);
        var resetToken = await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.TokenHash == tokenHash && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken)
            ?? throw new AppException("Invalid or expired reset token.");

        var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        resetToken.UsedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var context = _currentUserService.GetContext();
        if (context.UserId is null)
        {
            throw new UnauthorizedException("Unauthorized.");
        }

        var user = await _dbContext.Users
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value && x.Role.Name == UserRoleName.SuperAdmin, cancellationToken)
            ?? throw new UnauthorizedException("Unauthorized.");

        return new UserProfileDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.Name,
            CompanyName = user.Tenant.CompanyName
        };
    }

    public async Task ChangePasswordAsync(PortalAdminChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var context = _currentUserService.GetContext();
        if (context.UserId is null)
        {
            throw new UnauthorizedException("Unauthorized.");
        }

        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value && x.Role.Name == UserRoleName.SuperAdmin, cancellationToken)
            ?? throw new UnauthorizedException("Unauthorized.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            throw new AppException("Current password is incorrect.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user, string? ipAddress, CancellationToken cancellationToken)
    {
        var (accessToken, accessExpiresAt) = _tokenGenerator.GenerateAccessToken(user, user.Role.Name);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenGenerator.HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedByIp = ipAddress
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = accessExpiresAt,
            User = new UserProfileDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name,
                CompanyName = user.Tenant.CompanyName
            }
        };
    }
}

