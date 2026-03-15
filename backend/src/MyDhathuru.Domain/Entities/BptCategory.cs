using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class BptCategory : TenantEntity
{
    public BptCategoryCode Code { get; set; }
    public required string Name { get; set; }
    public BptClassificationGroup ClassificationGroup { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<BptMappingRule> MappingRules { get; set; } = new List<BptMappingRule>();
    public ICollection<BptAdjustment> Adjustments { get; set; } = new List<BptAdjustment>();
}
