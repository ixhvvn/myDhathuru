using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.PortalAdmin.Dtos;

public class PortalAdminEmailBusinessOptionDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public BusinessAccountStatus Status { get; set; }
    public int ActiveAdminCount { get; set; }
    public string? PrimaryAdminName { get; set; }
    public string? PrimaryAdminEmail { get; set; }
}

public class PortalAdminEmailCampaignQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminEmailCampaignListItemDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public AdminEmailAudienceMode AudienceMode { get; set; }
    public bool CcAdminUsers { get; set; }
    public bool IncludeDisabledBusinesses { get; set; }
    public int RequestedCompanyCount { get; set; }
    public int SentCompanyCount { get; set; }
    public int FailedCompanyCount { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public string? SentByName { get; set; }
}

public class PortalAdminSendEmailCampaignRequest
{
    public AdminEmailAudienceMode AudienceMode { get; set; } = AdminEmailAudienceMode.AllBusinesses;
    public IReadOnlyList<Guid> TenantIds { get; set; } = Array.Empty<Guid>();
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public bool CcAdminUsers { get; set; } = true;
    public bool IncludeDisabledBusinesses { get; set; }
}

public class PortalAdminEmailCampaignSendResultDto
{
    public Guid CampaignId { get; set; }
    public int RequestedCompanyCount { get; set; }
    public int SentCompanyCount { get; set; }
    public int FailedCompanyCount { get; set; }
    public IReadOnlyList<PortalAdminEmailCampaignSendCompanyResultDto> Results { get; set; } = Array.Empty<PortalAdminEmailCampaignSendCompanyResultDto>();
}

public class PortalAdminEmailCampaignSendCompanyResultDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public int CcAdminCount { get; set; }
    public AdminEmailRecipientStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
