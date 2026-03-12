using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class AdminAuditLog : AuditableEntity
{
    public Guid PerformedByUserId { get; set; }
    public AdminAuditActionType ActionType { get; set; }
    public required string TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }
    public Guid? RelatedTenantId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
}

