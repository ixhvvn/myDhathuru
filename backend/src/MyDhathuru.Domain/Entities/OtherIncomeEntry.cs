using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class OtherIncomeEntry : TenantEntity
{
    public required string EntryNumber { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CounterpartyName { get; set; }
    public required string Description { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}
