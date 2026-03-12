using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;
namespace MyDhathuru.Domain.Entities;
public class InvoicePayment : TenantEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
