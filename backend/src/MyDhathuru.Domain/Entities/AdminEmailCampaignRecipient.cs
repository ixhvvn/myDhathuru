using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class AdminEmailCampaignRecipient : AuditableEntity
{
    public Guid AdminEmailCampaignId { get; set; }
    public AdminEmailCampaign AdminEmailCampaign { get; set; } = null!;
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string? CcEmails { get; set; }
    public AdminEmailRecipientStatus Status { get; set; } = AdminEmailRecipientStatus.Sent;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}
