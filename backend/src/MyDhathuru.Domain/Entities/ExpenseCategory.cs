using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class ExpenseCategory : TenantEntity
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
    public BptCategoryCode BptCategoryCode { get; set; } = BptCategoryCode.Other;
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }
    public ICollection<ReceivedInvoice> ReceivedInvoices { get; set; } = new List<ReceivedInvoice>();
    public ICollection<ExpenseEntry> ExpenseEntries { get; set; } = new List<ExpenseEntry>();
    public ICollection<RentEntry> RentEntries { get; set; } = new List<RentEntry>();
}
