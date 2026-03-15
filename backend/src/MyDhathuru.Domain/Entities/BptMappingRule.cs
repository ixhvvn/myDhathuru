using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class BptMappingRule : TenantEntity
{
    public required string Name { get; set; }
    public BptSourceModule? SourceModule { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public SalesAdjustmentType? SalesAdjustmentType { get; set; }
    public RevenueCapitalType? RevenueCapitalClassification { get; set; }
    public Guid BptCategoryId { get; set; }
    public BptCategory BptCategory { get; set; } = null!;
    public int Priority { get; set; } = 100;
    public bool IsSystem { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
