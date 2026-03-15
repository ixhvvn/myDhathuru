using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Expenses.Dtos;

public class ExpenseLedgerQuery : PaginationQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public ExpenseSourceType? SourceType { get; set; }
    public bool PendingOnly { get; set; }
}

public class ExpenseSummaryQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

public class ExpenseLedgerRowDto
{
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public Guid? ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public BptCategoryCode BptCategoryCode { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
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

public class ExpenseEntryDetailDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
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

public class ExpenseSummaryBucketDto
{
    public string Label { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}

public class ExpenseSummaryDto
{
    public decimal TotalNetAmount { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalGrossAmount { get; set; }
    public decimal TotalPendingAmount { get; set; }
    public List<ExpenseSummaryBucketDto> ByCategory { get; set; } = new();
    public List<ExpenseSummaryBucketDto> ByMonth { get; set; } = new();
}

public class CreateManualExpenseEntryRequest
{
    public DateOnly TransactionDate { get; set; }
    public required string DocumentNumber { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    public required string PayeeName { get; set; }
    public string? Currency { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ClaimableTaxAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

public class UpdateManualExpenseEntryRequest : CreateManualExpenseEntryRequest
{
}
