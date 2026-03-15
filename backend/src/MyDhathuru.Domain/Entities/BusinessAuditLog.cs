using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class BusinessAuditLog : TenantEntity
{
    public Guid PerformedByUserId { get; set; }
    public BusinessAuditActionType ActionType { get; set; }
    public required string TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }
    public string? DetailsJson { get; set; }
    public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;
}
