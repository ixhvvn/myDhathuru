using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class DeliveryNoteItem : TenantEntity
{
    public Guid DeliveryNoteId { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public required string Details { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
    public decimal CashPayment { get; set; }
    public decimal VesselPayment { get; set; }
}
