using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;
namespace MyDhathuru.Domain.Entities;
public class Tenant : AuditableEntity
{
    public required string CompanyName { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhone { get; set; }
    public required string TinNumber { get; set; }
    public required string BusinessRegistrationNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public BusinessAccountStatus AccountStatus { get; set; } = BusinessAccountStatus.Active;
    public string? DisabledReason { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public Guid? DisabledByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
    public TenantSettings? Settings { get; set; }
}
