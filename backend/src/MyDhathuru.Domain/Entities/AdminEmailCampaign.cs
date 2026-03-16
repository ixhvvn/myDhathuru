using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class AdminEmailCampaign : AuditableEntity
{
    public Guid SentByUserId { get; set; }
    public AdminEmailAudienceMode AudienceMode { get; set; } = AdminEmailAudienceMode.AllBusinesses;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool CcAdminUsers { get; set; } = true;
    public bool IncludeDisabledBusinesses { get; set; }
    public int RequestedCompanyCount { get; set; }
    public int SentCompanyCount { get; set; }
    public int FailedCompanyCount { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AdminEmailCampaignRecipient> Recipients { get; set; } = new List<AdminEmailCampaignRecipient>();
}
