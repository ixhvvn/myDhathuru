using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class QuotationItem : TenantEntity
{
    public Guid QuotationId { get; set; }
    public Quotation Quotation { get; set; } = null!;
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}
