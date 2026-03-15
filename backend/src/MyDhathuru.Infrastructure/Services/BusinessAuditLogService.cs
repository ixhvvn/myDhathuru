using System.Text.Json;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Domain.Entities;
using MyDhathuru.Domain.Enums;
using MyDhathuru.Infrastructure.Persistence;

namespace MyDhathuru.Infrastructure.Services;

public class BusinessAuditLogService : IBusinessAuditLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICurrentUserService _currentUserService;

    public BusinessAuditLogService(
        ApplicationDbContext dbContext,
        ICurrentTenantService currentTenantService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentTenantService = currentTenantService;
        _currentUserService = currentUserService;
    }

    public async Task LogAsync(
        BusinessAuditActionType actionType,
        string targetType,
        string? targetId,
        string? targetName,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId ?? throw new UnauthorizedException("Tenant context is missing.");
        var userId = _currentUserService.GetContext().UserId ?? throw new UnauthorizedException("User context is missing.");

        _dbContext.BusinessAuditLogs.Add(new BusinessAuditLog
        {
            TenantId = tenantId,
            PerformedByUserId = userId,
            ActionType = actionType,
            TargetType = targetType,
            TargetId = targetId,
            TargetName = targetName,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
            PerformedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
