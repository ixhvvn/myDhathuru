using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class Supplier : TenantEntity
{
    public required string Name { get; set; }
    public string? TinNumber { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ReceivedInvoice> ReceivedInvoices { get; set; } = new List<ReceivedInvoice>();
}
