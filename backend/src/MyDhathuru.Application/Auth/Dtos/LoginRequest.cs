namespace MyDhathuru.Application.Auth.Dtos;

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}
