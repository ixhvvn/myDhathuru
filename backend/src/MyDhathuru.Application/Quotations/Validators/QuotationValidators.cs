using FluentValidation;
using MyDhathuru.Application.Quotations.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Quotations.Validators;

public class CreateQuotationRequestValidator : AbstractValidator<CreateQuotationRequest>
{
    public CreateQuotationRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DateIssued).NotEmpty();
        RuleFor(x => x.ValidUntil)
            .GreaterThanOrEqualTo(x => x.DateIssued)
            .When(x => x.ValidUntil.HasValue)
            .WithMessage("Valid until date cannot be earlier than the issued date.");
        RuleFor(x => x.PoNumber).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.TaxRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1)
            .When(x => x.TaxRate.HasValue);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new QuotationItemInputDtoValidator());
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

public class QuotationItemInputDtoValidator : AbstractValidator<QuotationItemInputDto>
{
    public QuotationItemInputDtoValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}
