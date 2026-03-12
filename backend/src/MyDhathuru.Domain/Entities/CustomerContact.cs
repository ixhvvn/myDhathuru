using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class CustomerContact : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Value { get; set; }
    public string Label { get; set; } = "Reference";
}
