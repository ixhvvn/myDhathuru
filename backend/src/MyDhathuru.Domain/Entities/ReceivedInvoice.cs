using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class ReceivedInvoice : TenantEntity
{
    public required string InvoiceNumber { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierTin { get; set; }
    public string? SupplierContactNumber { get; set; }
    public string? SupplierEmail { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Outlet { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public ReceivedInvoiceStatus PaymentStatus { get; set; } = ReceivedInvoiceStatus.Unpaid;
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptReference { get; set; }
    public string? SettlementReference { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountDetails { get; set; }
    public string? MiraTaxableActivityNumber { get; set; }
    public RevenueCapitalType RevenueCapitalClassification { get; set; } = RevenueCapitalType.Revenue;
    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = null!;
    public bool IsTaxClaimable { get; set; } = true;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public ICollection<ReceivedInvoiceItem> Items { get; set; } = new List<ReceivedInvoiceItem>();
    public ICollection<ReceivedInvoicePayment> Payments { get; set; } = new List<ReceivedInvoicePayment>();
    public ICollection<ReceivedInvoiceAttachment> Attachments { get; set; } = new List<ReceivedInvoiceAttachment>();
}
