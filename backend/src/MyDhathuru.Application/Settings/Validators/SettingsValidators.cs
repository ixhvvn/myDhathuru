using FluentValidation;
using MyDhathuru.Application.Common.Validation;
using MyDhathuru.Application.Settings.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Settings.Validators;

public class UpdateTenantSettingsRequestValidator : AbstractValidator<UpdateTenantSettingsRequest>
{
    public UpdateTenantSettingsRequestValidator()
    {
        RuleFor(x => x.Username)
            .MaximumLength(120)
            .Matches(ValidationPatterns.Name)
            .When(x => !string.IsNullOrWhiteSpace(x.Username))
            .WithMessage("Username must not contain numbers.");
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Company name must not contain numbers.");
        RuleFor(x => x.CompanyEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.CompanyPhone)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .WithMessage("Company phone number must contain only digits (optional leading +).");
        RuleFor(x => x.TinNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BusinessRegistrationNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InvoicePrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DeliveryNotePrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.QuotePrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PurchaseOrderPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ReceivedInvoicePrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PaymentVoucherPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.RentEntryPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.WarningFormPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.StatementPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.SalarySlipPrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DefaultTaxRate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1);
        RuleFor(x => x.DefaultTaxRate)
            .GreaterThan(0)
            .When(x => x.IsTaxApplicable)
            .WithMessage("Default tax rate must be greater than 0 when tax is applicable.");
        RuleFor(x => x.DefaultDueDays).InclusiveBetween(0, 120);
        RuleFor(x => x.DefaultCurrency)
            .NotEmpty()
            .Must(BeValidCurrency)
            .WithMessage("Default currency must be MVR or USD.");
        RuleFor(x => x.TaxableActivityNumber).MaximumLength(50);
        RuleFor(x => x.BmlMvrAccountName).MaximumLength(200);
        RuleFor(x => x.BmlMvrAccountNumber).MaximumLength(100);
        RuleFor(x => x.BmlUsdAccountName).MaximumLength(200);
        RuleFor(x => x.BmlUsdAccountNumber).MaximumLength(100);
        RuleFor(x => x.MibMvrAccountName).MaximumLength(200);
        RuleFor(x => x.MibMvrAccountNumber).MaximumLength(100);
        RuleFor(x => x.MibUsdAccountName).MaximumLength(200);
        RuleFor(x => x.MibUsdAccountNumber).MaximumLength(100);
        RuleFor(x => x.InvoiceOwnerName)
            .MaximumLength(200)
            .Matches(ValidationPatterns.Name)
            .When(x => !string.IsNullOrWhiteSpace(x.InvoiceOwnerName))
            .WithMessage("Invoice owner name must not contain numbers.");
        RuleFor(x => x.InvoiceOwnerIdCard).MaximumLength(100);
        RuleFor(x => x.LogoUrl)
            .MaximumLength(400)
            .Must(BeSupportedImageUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("Logo URL must use HTTP/HTTPS, a root static path, or start with /uploads.");
        RuleFor(x => x.CompanyStampUrl)
            .MaximumLength(400)
            .Must(BeSupportedImageUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CompanyStampUrl))
            .WithMessage("Company stamp URL must use HTTP/HTTPS, a root static path, or start with /uploads.");
        RuleFor(x => x.CompanySignatureUrl)
            .MaximumLength(400)
            .Must(BeSupportedImageUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CompanySignatureUrl))
            .WithMessage("Company signature URL must use HTTP/HTTPS, a root static path, or start with /uploads.");
    }

    private static bool BeValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return false;
        }

        return Enum.TryParse<CurrencyCode>(currency.Trim(), true, out _);
    }

    private static bool BeSupportedImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        if (candidate.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.StartsWith("/", StringComparison.Ordinal))
        {
            var normalizedPath = candidate.Split('?', 2)[0].Split('#', 2)[0];
            return normalizedPath.Length > 1
                && !normalizedPath.Contains("..", StringComparison.Ordinal)
                && normalizedPath.All(ch => char.IsLetterOrDigit(ch) || ch is '/' or '_' or '-' or '.');
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}
