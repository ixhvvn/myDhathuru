using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class ReceivedInvoicePayment : TenantEntity
{
    public Guid ReceivedInvoiceId { get; set; }
    public ReceivedInvoice ReceivedInvoice { get; set; } = null!;
    public Guid? PaymentVoucherId { get; set; }
    public PaymentVoucher? PaymentVoucher { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
