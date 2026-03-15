using FluentValidation;
using MyDhathuru.Application.Invoices.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Invoices.Validators;

public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DateIssued).NotEmpty();
        RuleFor(x => x.PoNumber).MaximumLength(100);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.TaxRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1)
            .When(x => x.TaxRate.HasValue);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new InvoiceItemInputDtoValidator());
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

public class InvoiceItemInputDtoValidator : AbstractValidator<InvoiceItemInputDto>
{
    public InvoiceItemInputDtoValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public class ReceiveInvoicePaymentRequestValidator : AbstractValidator<ReceiveInvoicePaymentRequest>
{
    public ReceiveInvoicePaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
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
