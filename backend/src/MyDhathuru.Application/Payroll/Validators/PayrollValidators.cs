using FluentValidation;
using MyDhathuru.Application.Common.Validation;
using MyDhathuru.Application.Payroll.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Payroll.Validators;

public class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty().MaximumLength(40);
        RuleFor(x => x.StaffName)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Staff name must not contain numbers.");
        RuleFor(x => x.Designation).MaximumLength(120);
        RuleFor(x => x.WorkSite).MaximumLength(120);
        RuleFor(x => x.BankName)
            .Must(BeValidBank)
            .When(x => !string.IsNullOrWhiteSpace(x.BankName))
            .WithMessage("Bank must be BML or MIB.");
        RuleFor(x => x.AccountName)
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountName))
            .WithMessage("Account name must not contain numbers.");
        RuleFor(x => x.AccountNumber)
            .MaximumLength(100)
            .Matches(ValidationPatterns.AccountNumber)
            .When(x => !string.IsNullOrWhiteSpace(x.AccountNumber))
            .WithMessage("Account number must contain only digits.");
        RuleFor(x => x)
            .Must(HaveCompleteBankDetails)
            .WithMessage("Select bank and provide account name and account number together.");
    }

    private static bool BeValidBank(string? bankName)
    {
        if (string.IsNullOrWhiteSpace(bankName))
        {
            return true;
        }

        return Enum.TryParse<BankCode>(bankName.Trim(), true, out _);
    }

    private static bool HaveCompleteBankDetails(CreateStaffRequest request)
    {
        var hasBank = !string.IsNullOrWhiteSpace(request.BankName);
        var hasAccountName = !string.IsNullOrWhiteSpace(request.AccountName);
        var hasAccountNumber = !string.IsNullOrWhiteSpace(request.AccountNumber);

        if (!hasBank && !hasAccountName && !hasAccountNumber)
        {
            return true;
        }

        return hasBank && hasAccountName && hasAccountNumber;
    }
}

public class CreatePayrollPeriodRequestValidator : AbstractValidator<CreatePayrollPeriodRequest>
{
    public CreatePayrollPeriodRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 3000);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}
