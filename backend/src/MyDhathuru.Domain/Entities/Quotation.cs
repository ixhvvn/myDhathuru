using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class Quotation : TenantEntity
{
    public required string QuotationNo { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? CourierVesselId { get; set; }
    public Vessel? CourierVessel { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string? PoNumber { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public DocumentEmailStatus EmailStatus { get; set; } = DocumentEmailStatus.Pending;
    public DateTimeOffset? LastEmailedAt { get; set; }
    public string? LastEmailedTo { get; set; }
    public string? LastEmailedCc { get; set; }
    public string? Notes { get; set; }
    public DeliveryNote? ConvertedDeliveryNote { get; set; }
    public Invoice? ConvertedInvoice { get; set; }
    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
}
