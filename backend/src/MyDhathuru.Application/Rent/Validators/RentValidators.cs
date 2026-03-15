using FluentValidation;
using MyDhathuru.Application.Rent.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Rent.Validators;

public class CreateRentEntryRequestValidator : AbstractValidator<CreateRentEntryRequest>
{
    public CreateRentEntryRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.PropertyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PayTo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }

    private static bool BeValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return true;
        }

        return Enum.TryParse<CurrencyCode>(currency.Trim(), true, out _);
    }
}

public class UpdateRentEntryRequestValidator : AbstractValidator<UpdateRentEntryRequest>
{
    public UpdateRentEntryRequestValidator()
    {
        Include(new CreateRentEntryRequestValidator());
    }
}
