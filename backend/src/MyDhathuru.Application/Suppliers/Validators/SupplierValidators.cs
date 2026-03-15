using FluentValidation;
using MyDhathuru.Application.Common.Validation;
using MyDhathuru.Application.Suppliers.Dtos;

namespace MyDhathuru.Application.Suppliers.Validators;

public class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
        RuleFor(x => x.TinNumber).MaximumLength(100);
        RuleFor(x => x.ContactNumber)
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactNumber))
            .WithMessage("Contact number must contain only digits (optional leading +).");
        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(400);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        Include(new CreateSupplierRequestValidator());
    }
}
