using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IBusinessAuditLogService
{
    Task LogAsync(
        BusinessAuditActionType actionType,
        string targetType,
        string? targetId,
        string? targetName,
        object? details = null,
        CancellationToken cancellationToken = default);
}
