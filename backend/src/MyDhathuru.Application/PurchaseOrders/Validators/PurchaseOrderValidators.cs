using FluentValidation;
using MyDhathuru.Application.PurchaseOrders.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.PurchaseOrders.Validators;

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.DateIssued).NotEmpty();
        RuleFor(x => x.RequiredDate)
            .GreaterThanOrEqualTo(x => x.DateIssued)
            .When(x => x.RequiredDate.HasValue)
            .WithMessage("Required date cannot be earlier than the issued date.");
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.TaxRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1)
            .When(x => x.TaxRate.HasValue);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new PurchaseOrderItemInputDtoValidator());
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

public class PurchaseOrderItemInputDtoValidator : AbstractValidator<PurchaseOrderItemInputDto>
{
    public PurchaseOrderItemInputDtoValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}
