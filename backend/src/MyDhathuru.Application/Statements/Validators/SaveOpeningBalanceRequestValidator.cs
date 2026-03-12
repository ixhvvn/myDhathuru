using FluentValidation;
using MyDhathuru.Application.Statements.Dtos;

namespace MyDhathuru.Application.Statements.Validators;

public class SaveOpeningBalanceRequestValidator : AbstractValidator<SaveOpeningBalanceRequest>
{
    public SaveOpeningBalanceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 3000);
        RuleFor(x => x.OpeningBalanceMvr).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OpeningBalanceUsd).GreaterThanOrEqualTo(0);
    }
}
