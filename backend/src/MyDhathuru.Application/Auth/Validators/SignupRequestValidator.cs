using FluentValidation;
using MyDhathuru.Application.Auth.Dtos;
using MyDhathuru.Application.Common.Validation;

namespace MyDhathuru.Application.Auth.Validators;

public class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    public SignupRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Company name must not contain numbers.");
        RuleFor(x => x.CompanyEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.CompanyPhoneNumber)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .WithMessage("Company phone number must contain only digits (optional leading +).");
        RuleFor(x => x.CompanyTinNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BusinessRegistrationNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdminFullName)
            .NotEmpty()
            .MaximumLength(150)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Admin full name must not contain numbers.");
        RuleFor(x => x.AdminUserEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password);
    }
}
