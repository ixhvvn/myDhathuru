using FluentValidation;
using MyDhathuru.Application.Expenses.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Expenses.Validators;

public class CreateManualExpenseEntryRequestValidator : AbstractValidator<CreateManualExpenseEntryRequest>
{
    public CreateManualExpenseEntryRequestValidator()
    {
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(60);
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.PayeeName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.NetAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ClaimableTaxAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PendingAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
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

public class UpdateManualExpenseEntryRequestValidator : AbstractValidator<UpdateManualExpenseEntryRequest>
{
    public UpdateManualExpenseEntryRequestValidator()
    {
        Include(new CreateManualExpenseEntryRequestValidator());
    }
}
