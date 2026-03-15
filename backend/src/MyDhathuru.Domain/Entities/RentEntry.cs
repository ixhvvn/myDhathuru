using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class RentEntry : TenantEntity
{
    public required string RentNumber { get; set; }
    public DateOnly Date { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string PayTo { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}
