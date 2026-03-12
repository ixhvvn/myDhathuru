using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Auth.Dtos;

public class SignupRequestSubmittedDto
{
    public Guid RequestId { get; set; }
    public SignupRequestStatus Status { get; set; }
}

