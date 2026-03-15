using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;
namespace MyDhathuru.Domain.Entities;
public class Invoice : TenantEntity
{
    public required string InvoiceNo { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? QuotationId { get; set; }
    public Quotation? Quotation { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public DeliveryNote? DeliveryNote { get; set; }
    public Guid? CourierVesselId { get; set; }
    public Vessel? CourierVessel { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly DateDue { get; set; }
    public string? PoNumber { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? Notes { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
}
