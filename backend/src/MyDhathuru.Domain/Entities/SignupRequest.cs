using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class SignupRequest : AuditableEntity
{
    public required string CompanyName { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhone { get; set; }
    public required string TinNumber { get; set; }
    public required string BusinessRegistrationNumber { get; set; }
    public required string RequestedByName { get; set; }
    public required string RequestedByEmail { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public SignupRequestStatus Status { get; set; } = SignupRequestStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? ApprovedTenantId { get; set; }
    public string? ReviewNotes { get; set; }
}

