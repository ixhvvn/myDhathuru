using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class Role : AuditableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
