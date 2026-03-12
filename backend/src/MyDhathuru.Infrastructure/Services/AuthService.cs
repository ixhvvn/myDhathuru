using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Configuration;
using MyDhathuru.Infrastructure.Persistence;
using MyDhathuru.Infrastructure.Security;
using SignupRequestDto = MyDhathuru.Application.Auth.Dtos.SignupRequest;
using SignupRequestEntity = MyDhathuru.Domain.Entities.SignupRequest;

namespace MyDhathuru.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
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

    public async Task<SignupRequestSubmittedDto> SignupAsync(SignupRequestDto request, CancellationToken cancellationToken = default)
    {
        var companyEmail = request.CompanyEmail.Trim().ToLowerInvariant();
        var email = request.AdminUserEmail.Trim().ToLowerInvariant();

        var userEmailExists = await _dbContext.Users.IgnoreQueryFilters().AnyAsync(x => x.Email == email && !x.IsDeleted, cancellationToken);
        if (userEmailExists)
        {
            throw new AppException("User email already exists.");
        }

        var tenantExists = await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(
            x => x.CompanyEmail.ToLower() == companyEmail || x.BusinessRegistrationNumber.ToLower() == request.BusinessRegistrationNumber.Trim().ToLower(),
            cancellationToken);
        if (tenantExists)
        {
            throw new AppException("Business already exists.");
        }

        var pendingRequestExists = await _dbContext.SignupRequests
            .AnyAsync(
                x => x.Status == SignupRequestStatus.Pending
                    && (x.CompanyEmail.ToLower() == companyEmail || x.RequestedByEmail.ToLower() == email),
                cancellationToken);
        if (pendingRequestExists)
        {
            throw new AppException("A pending signup request already exists for this email.");
        }

        var (hash, salt) = _passwordHasher.HashPassword(request.Password);

        var signupRequest = new SignupRequestEntity
        {
            CompanyName = request.CompanyName.Trim(),
            CompanyEmail = companyEmail,
            CompanyPhone = request.CompanyPhoneNumber.Trim(),
            TinNumber = request.CompanyTinNumber.Trim(),
            BusinessRegistrationNumber = request.BusinessRegistrationNumber.Trim(),
            RequestedByName = request.AdminFullName.Trim(),
            RequestedByEmail = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Status = SignupRequestStatus.Pending,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        _dbContext.SignupRequests.Add(signupRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SignupRequestSubmittedDto
        {
            RequestId = signupRequest.Id,
            Status = signupRequest.Status
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var loginIdentifier = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Email == loginIdentifier && x.IsActive && x.Role.Name != UserRoleName.SuperAdmin, cancellationToken);

        if (user is null)
        {
            // Allow admins to log in using company email as a convenience identifier.
            var companyAdmins = await _dbContext.Users
                .Include(x => x.Role)
                .Include(x => x.Tenant)
                .Where(x =>
                    x.IsActive
                    && x.Role.Name == UserRoleName.Admin
                    && x.Tenant.CompanyEmail.ToLower() == loginIdentifier)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            user = companyAdmins.FirstOrDefault(x => _passwordHasher.Verify(request.Password, x.PasswordHash, x.PasswordSalt));
        }

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedException("Invalid credentials.");
        }

        if (!user.Tenant.IsActive || user.Tenant.AccountStatus == BusinessAccountStatus.Disabled)
        {
            throw new UnauthorizedException("Your business account is temporarily disabled. Please contact portal admin.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, user.Role.Name, ipAddress, cancellationToken);
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

        if (!existing.IsActive)
        {
            throw new UnauthorizedException("Refresh token expired or revoked.");
        }

        if (existing.User.Role.Name == UserRoleName.SuperAdmin)
        {
            throw new UnauthorizedException("Invalid refresh token for this login.");
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
                CompanyName = user.Tenant?.CompanyName ?? string.Empty
            }
        };
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive && x.Role.Name != UserRoleName.SuperAdmin, cancellationToken);
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
        await _notificationService.SendPasswordResetAsync(email, token, isPortalAdmin: false, cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email && x.IsActive && x.Role.Name != UserRoleName.SuperAdmin, cancellationToken)
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

    public async Task<UserProfileDto> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default)
    {
        var context = _currentUserService.GetContext();
        if (context.UserId is null)
        {
            throw new UnauthorizedException("Unauthorized.");
        }

        var user = await _dbContext.Users
            .Include(x => x.Role)
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.Id == context.UserId.Value, cancellationToken)
            ?? throw new NotFoundException("User not found.");

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

    private async Task<AuthResponseDto> BuildAuthResponseAsync(User user, string roleName, string? ipAddress, CancellationToken cancellationToken)
    {
        var (accessToken, accessExpiresAt) = _tokenGenerator.GenerateAccessToken(user, roleName);
        var refreshToken = _tokenGenerator.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenGenerator.HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedByIp = ipAddress
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == user.TenantId, cancellationToken);

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
                Role = roleName,
                CompanyName = tenant?.CompanyName ?? string.Empty
            }
        };
    }

    private async Task<Role> EnsureRoleAsync(string roleName, string description, CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role
        {
            Name = roleName,
            Description = description
        };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }
}
