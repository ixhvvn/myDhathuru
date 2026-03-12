using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class BusinessCustomRate : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public decimal SoftwareFee { get; set; }
    public decimal VesselFee { get; set; }
    public decimal StaffFee { get; set; }
    public string Currency { get; set; } = "MVR";
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}
