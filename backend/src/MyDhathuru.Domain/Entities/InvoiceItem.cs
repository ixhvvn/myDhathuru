using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class InvoiceItem : TenantEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public required string Description { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Total { get; set; }
}
