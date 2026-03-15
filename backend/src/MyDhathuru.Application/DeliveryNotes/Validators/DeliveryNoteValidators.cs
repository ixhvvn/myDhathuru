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
        RuleFor(x => x.VesselPaymentFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VesselPaymentInvoiceNumber).MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new DeliveryNoteItemInputDtoValidator());

        When(
            x => x.VesselPaymentFee > 0 || !string.IsNullOrWhiteSpace(x.VesselPaymentInvoiceNumber),
            () =>
            {
                RuleFor(x => x.VesselId)
                    .NotEmpty()
                    .WithMessage("Courier is required when vessel payment is recorded.");
                RuleFor(x => x.VesselPaymentFee)
                    .GreaterThan(0)
                    .WithMessage("Vessel payment fee must be greater than zero.");
                RuleFor(x => x.VesselPaymentInvoiceNumber)
                    .NotEmpty()
                    .WithMessage("Vessel payment invoice number is required.");
            });
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
