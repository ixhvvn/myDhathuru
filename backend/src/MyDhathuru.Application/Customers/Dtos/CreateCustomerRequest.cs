namespace MyDhathuru.Application.Customers.Dtos;

public class CreateCustomerRequest
{
    public required string Name { get; set; }
    public string? TinNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public List<string> References { get; set; } = new();
}
