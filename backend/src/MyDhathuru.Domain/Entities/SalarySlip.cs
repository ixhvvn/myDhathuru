using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class SalarySlip : TenantEntity
{
    public required string SlipNo { get; set; }
    public Guid PayrollEntryId { get; set; }
    public PayrollEntry PayrollEntry { get; set; } = null!;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
}
