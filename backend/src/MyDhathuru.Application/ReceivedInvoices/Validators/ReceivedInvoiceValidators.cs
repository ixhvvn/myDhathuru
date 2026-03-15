using FluentValidation;
using MyDhathuru.Application.ReceivedInvoices.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.ReceivedInvoices.Validators;

public class CreateReceivedInvoiceRequestValidator : AbstractValidator<CreateReceivedInvoiceRequest>
{
    public CreateReceivedInvoiceRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.InvoiceNumber).NotEmpty().MaximumLength(60);
        RuleFor(x => x.InvoiceDate).NotEmpty();
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.Currency)
            .Must(BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GstRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1)
            .When(x => x.GstRate.HasValue);
        RuleFor(x => x.Outlet).MaximumLength(160);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.ReceiptReference).MaximumLength(120);
        RuleFor(x => x.SettlementReference).MaximumLength(120);
        RuleFor(x => x.BankName).MaximumLength(120);
        RuleFor(x => x.BankAccountDetails).MaximumLength(200);
        RuleFor(x => x.MiraTaxableActivityNumber).MaximumLength(50);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new ReceivedInvoiceItemInputDtoValidator());
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

public class UpdateReceivedInvoiceRequestValidator : AbstractValidator<UpdateReceivedInvoiceRequest>
{
    public UpdateReceivedInvoiceRequestValidator()
    {
        Include(new CreateReceivedInvoiceRequestValidator());
    }
}

public class ReceivedInvoiceItemInputDtoValidator : AbstractValidator<ReceivedInvoiceItemInputDto>
{
    public ReceivedInvoiceItemInputDtoValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Uom).MaximumLength(50);
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GstRate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1);
    }
}

public class RecordReceivedInvoicePaymentRequestValidator : AbstractValidator<RecordReceivedInvoicePaymentRequest>
{
    public RecordReceivedInvoicePaymentRequestValidator()
    {
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reference).MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
