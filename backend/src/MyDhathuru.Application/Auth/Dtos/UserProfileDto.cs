namespace MyDhathuru.Application.Auth.Dtos;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}
