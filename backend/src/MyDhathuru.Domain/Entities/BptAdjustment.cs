using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class BptAdjustment : TenantEntity
{
    public required string AdjustmentNumber { get; set; }
    public DateOnly TransactionDate { get; set; }
    public required string Description { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public Guid BptCategoryId { get; set; }
    public BptCategory BptCategory { get; set; } = null!;
    public string? Notes { get; set; }
}
