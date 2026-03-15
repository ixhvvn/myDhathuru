using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class ExpenseEntry : TenantEntity
{
    public ExpenseSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public required string DocumentNumber { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal ClaimableTaxAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}
