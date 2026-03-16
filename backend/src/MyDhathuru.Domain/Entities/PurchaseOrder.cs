using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class PurchaseOrder : TenantEntity
{
    public required string PurchaseOrderNo { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? CourierVesselId { get; set; }
    public Vessel? CourierVessel { get; set; }
    public DateOnly DateIssued { get; set; }
    public DateOnly RequiredDate { get; set; }
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
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
