using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.PortalAdmin.Dtos;

public class PortalAdminDashboardDto
{
    public int TotalBusinesses { get; set; }
    public int PendingSignupRequests { get; set; }
    public int ActiveBusinesses { get; set; }
    public int DisabledBusinesses { get; set; }
    public int TotalStaffAcrossBusinesses { get; set; }
    public int TotalVesselsAcrossBusinesses { get; set; }
    public IReadOnlyList<PortalAdminRecentSignupRequestDto> RecentSignupRequests { get; set; } = Array.Empty<PortalAdminRecentSignupRequestDto>();
    public IReadOnlyList<PortalAdminAuditLogDto> RecentActions { get; set; } = Array.Empty<PortalAdminAuditLogDto>();
}

public class PortalAdminRecentSignupRequestDto
{
    public Guid Id { get; set; }
    public DateTimeOffset RequestDate { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public SignupRequestStatus Status { get; set; }
}

public class SignupRequestListQuery
{
    public string? Search { get; set; }
    public SignupRequestStatus? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class SignupRequestListItemDto
{
    public Guid Id { get; set; }
    public DateTimeOffset RequestDate { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string TinNumber { get; set; } = string.Empty;
    public string BusinessRegistrationNumber { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string RequestedByEmail { get; set; } = string.Empty;
    public SignupRequestStatus Status { get; set; }
}

public class SignupRequestDetailDto : SignupRequestListItemDto
{
    public string? RejectionReason { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public Guid? ApprovedTenantId { get; set; }
}

public class SignupRequestCountsDto
{
    public int Pending { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
}

public class ApproveSignupRequest
{
    public string? Notes { get; set; }
}

public class RejectSignupRequest
{
    public required string RejectionReason { get; set; }
}

public class PortalAdminBusinessListQuery
{
    public string? Search { get; set; }
    public BusinessAccountStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminBusinessListItemDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string TinNumber { get; set; } = string.Empty;
    public string BusinessRegistrationNumber { get; set; } = string.Empty;
    public BusinessAccountStatus Status { get; set; }
    public int StaffCount { get; set; }
    public int VesselCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
}

public class PortalAdminBusinessDetailDto : PortalAdminBusinessListItemDto
{
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? DisabledReason { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public int CustomerCount { get; set; }
    public int InvoiceCount { get; set; }
    public PortalAdminBusinessUserDto? PrimaryAdmin { get; set; }
}

public class PortalAdminBusinessUserDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public class PortalAdminBusinessUsersQuery
{
    public Guid? TenantId { get; set; }
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminUpdateBusinessLoginRequest
{
    public required string AdminFullName { get; set; }
    public required string AdminLoginEmail { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhone { get; set; }
}

public class PortalAdminSetBusinessStatusRequest
{
    public string? Reason { get; set; }
}

public class PortalAdminSendResetLinkRequest
{
    public string? AdminEmail { get; set; }
}

public class PortalAdminAuditLogQuery
{
    public AdminAuditActionType? ActionType { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminAuditLogDto
{
    public Guid Id { get; set; }
    public DateTimeOffset PerformedAt { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string? TargetName { get; set; }
    public Guid? RelatedTenantId { get; set; }
    public string? PerformedByName { get; set; }
    public string? Details { get; set; }
}

public class PortalAdminChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
    public required string ConfirmPassword { get; set; }
}

