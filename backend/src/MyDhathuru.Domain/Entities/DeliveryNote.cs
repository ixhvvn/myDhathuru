using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class DeliveryNote : TenantEntity
{
    public required string DeliveryNoteNo { get; set; }
    public string? PoNumber { get; set; }
    public string Currency { get; set; } = "MVR";
    public DateOnly Date { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? VesselId { get; set; }
    public Vessel? Vessel { get; set; }
    public string? Notes { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public ICollection<DeliveryNoteItem> Items { get; set; } = new List<DeliveryNoteItem>();
    public decimal TotalAmount => Items.Sum(x => x.Total);
}
