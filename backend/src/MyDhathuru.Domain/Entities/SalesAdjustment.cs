using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class SalesAdjustment : TenantEntity
{
    public required string AdjustmentNumber { get; set; }
    public SalesAdjustmentType AdjustmentType { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public Invoice? RelatedInvoice { get; set; }
    public string? RelatedInvoiceNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}
