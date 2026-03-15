using FluentValidation;
using MyDhathuru.Application.PaymentVouchers.Dtos;

namespace MyDhathuru.Application.PaymentVouchers.Validators;

public class CreatePaymentVoucherRequestValidator : AbstractValidator<CreatePaymentVoucherRequest>
{
    public CreatePaymentVoucherRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.PayTo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Details).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AccountNumber).MaximumLength(120);
        RuleFor(x => x.ChequeNumber).MaximumLength(120);
        RuleFor(x => x.Bank).MaximumLength(120);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.AmountInWords).MaximumLength(500);
        RuleFor(x => x.ApprovedBy).MaximumLength(160);
        RuleFor(x => x.ReceivedBy).MaximumLength(160);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UpdatePaymentVoucherRequestValidator : AbstractValidator<UpdatePaymentVoucherRequest>
{
    public UpdatePaymentVoucherRequestValidator()
    {
        Include(new CreatePaymentVoucherRequestValidator());
    }
}

public class UpdatePaymentVoucherStatusRequestValidator : AbstractValidator<UpdatePaymentVoucherStatusRequest>
{
    public UpdatePaymentVoucherStatusRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}
