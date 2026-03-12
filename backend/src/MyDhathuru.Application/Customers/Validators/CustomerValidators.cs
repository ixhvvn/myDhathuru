using FluentValidation;
using MyDhathuru.Application.Common.Validation;
using MyDhathuru.Application.Customers.Dtos;

namespace MyDhathuru.Application.Customers.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Customer name must not contain numbers.");
        RuleFor(x => x.TinNumber).MaximumLength(100);
        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Phone number must contain only digits (optional leading +).");
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CreateVesselRequestValidator : AbstractValidator<CreateVesselRequest>
{
    public CreateVesselRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.RegistrationNumber).MaximumLength(100);
        RuleFor(x => x.PassengerCapacity).GreaterThanOrEqualTo(0).When(x => x.PassengerCapacity.HasValue);
        RuleFor(x => x.VesselType).MaximumLength(100);
        RuleFor(x => x.HomePort).MaximumLength(120);
        RuleFor(x => x.OwnerName)
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .When(x => !string.IsNullOrWhiteSpace(x.OwnerName))
            .WithMessage("Owner name must not contain numbers.");
        RuleFor(x => x.ContactPhone)
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone))
            .WithMessage("Contact phone must contain only digits (optional leading +).");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
