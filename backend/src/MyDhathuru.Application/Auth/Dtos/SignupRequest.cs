namespace MyDhathuru.Application.Auth.Dtos;

public class SignupRequest
{
    public required string CompanyName { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhoneNumber { get; set; }
    public required string CompanyTinNumber { get; set; }
    public required string BusinessRegistrationNumber { get; set; }

    public required string AdminFullName { get; set; }
    public required string AdminUserEmail { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
}
