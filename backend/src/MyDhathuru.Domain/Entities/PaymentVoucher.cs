using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class PaymentVoucher : TenantEntity
{
    public required string VoucherNumber { get; set; }
    public DateOnly Date { get; set; }
    public required string PayTo { get; set; }
    public required string Details { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Transfer;
    public string? AccountNumber { get; set; }
    public string? ChequeNumber { get; set; }
    public string? Bank { get; set; }
    public decimal Amount { get; set; }
    public string AmountInWords { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? ReceivedBy { get; set; }
    public Guid? LinkedReceivedInvoiceId { get; set; }
    public ReceivedInvoice? LinkedReceivedInvoice { get; set; }
    public Guid? LinkedExpenseEntryId { get; set; }
    public ExpenseEntry? LinkedExpenseEntry { get; set; }
    public string? Notes { get; set; }
    public PaymentVoucherStatus Status { get; set; } = PaymentVoucherStatus.Draft;
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public ICollection<ReceivedInvoicePayment> ReceivedInvoicePayments { get; set; } = new List<ReceivedInvoicePayment>();
}
