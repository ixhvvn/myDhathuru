using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class Customer : TenantEntity
{
    public required string Name { get; set; }
    public string? TinNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public ICollection<CustomerContact> Contacts { get; set; } = new List<CustomerContact>();
}
