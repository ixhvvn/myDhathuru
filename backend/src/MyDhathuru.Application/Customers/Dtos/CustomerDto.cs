namespace MyDhathuru.Application.Customers.Dtos;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TinNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<string> References { get; set; } = Array.Empty<string>();
}
