using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class ReceivedInvoiceItem : TenantEntity
{
    public Guid ReceivedInvoiceId { get; set; }
    public ReceivedInvoice ReceivedInvoice { get; set; } = null!;
    public required string Description { get; set; }
    public string? Uom { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
}
