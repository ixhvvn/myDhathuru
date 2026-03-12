using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class AdminInvoiceLineItem : AuditableEntity
{
    public Guid AdminInvoiceId { get; set; }
    public AdminInvoice AdminInvoice { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}
