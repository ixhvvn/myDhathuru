using FluentValidation;
using MyDhathuru.Application.DeliveryNotes.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.DeliveryNotes.Validators;

public class CreateDeliveryNoteRequestValidator : AbstractValidator<CreateDeliveryNoteRequest>
{
    public CreateDeliveryNoteRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.PoNumber).MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new DeliveryNoteItemInputDtoValidator());
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

public class DeliveryNoteItemInputDtoValidator : AbstractValidator<DeliveryNoteItemInputDto>
{
    public DeliveryNoteItemInputDtoValidator()
    {
        RuleFor(x => x.Details).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CashPayment).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VesselPayment).GreaterThanOrEqualTo(0);
    }
}
