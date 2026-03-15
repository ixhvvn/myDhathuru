using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class PurchaseOrderItem : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}
