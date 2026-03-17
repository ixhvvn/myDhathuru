using FluentValidation;
using MyDhathuru.Application.Common.Validation;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.PortalAdmin.Validators;

public class SignupRequestListQueryValidator : AbstractValidator<SignupRequestListQuery>
{
    public SignupRequestListQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("FromDate must be before or equal to ToDate.");
    }
}

public class RejectSignupRequestValidator : AbstractValidator<RejectSignupRequest>
{
    public RejectSignupRequestValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
    }
}

public class PortalAdminBusinessListQueryValidator : AbstractValidator<PortalAdminBusinessListQuery>
{
    public PortalAdminBusinessListQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
    }
}

public class PortalAdminUpdateBusinessLoginRequestValidator : AbstractValidator<PortalAdminUpdateBusinessLoginRequest>
{
    public PortalAdminUpdateBusinessLoginRequestValidator()
    {
        RuleFor(x => x.AdminFullName)
            .NotEmpty()
            .MaximumLength(150)
            .Matches(ValidationPatterns.Name)
            .WithMessage("Admin full name must not contain numbers.");
        RuleFor(x => x.AdminLoginEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.CompanyEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.CompanyPhone)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(ValidationPatterns.Phone)
            .WithMessage("Company phone must contain only digits (optional leading +).");
    }
}

public class PortalAdminSetBusinessStatusRequestValidator : AbstractValidator<PortalAdminSetBusinessStatusRequest>
{
    public PortalAdminSetBusinessStatusRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public class PortalAdminSetBusinessDataTestingRequestValidator : AbstractValidator<PortalAdminSetBusinessDataTestingRequest>
{
    public PortalAdminSetBusinessDataTestingRequestValidator()
    {
    }
}

public class PortalAdminSendResetLinkRequestValidator : AbstractValidator<PortalAdminSendResetLinkRequest>
{
    public PortalAdminSendResetLinkRequestValidator()
    {
        RuleFor(x => x.AdminEmail).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.AdminEmail));
    }
}

public class PortalAdminBusinessUsersQueryValidator : AbstractValidator<PortalAdminBusinessUsersQuery>
{
    public PortalAdminBusinessUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
    }
}

public class PortalAdminAuditLogQueryValidator : AbstractValidator<PortalAdminAuditLogQuery>
{
    public PortalAdminAuditLogQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(160);
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("FromDate must be before or equal to ToDate.");
    }
}

public class PortalAdminEmailCampaignQueryValidator : AbstractValidator<PortalAdminEmailCampaignQuery>
{
    public PortalAdminEmailCampaignQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class PortalAdminSendEmailCampaignRequestValidator : AbstractValidator<PortalAdminSendEmailCampaignRequest>
{
    public PortalAdminSendEmailCampaignRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
        RuleForEach(x => x.TenantIds).NotEmpty();
        RuleFor(x => x)
            .Must(x => x.AudienceMode != Domain.Enums.AdminEmailAudienceMode.SelectedBusinesses || x.TenantIds.Count > 0)
            .WithMessage("Select at least one business when sending to selected businesses.");
    }
}

public class PortalAdminChangePasswordRequestValidator : AbstractValidator<PortalAdminChangePasswordRequest>
{
    public PortalAdminChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}

public class UpdatePortalAdminBillingSettingsRequestValidator : AbstractValidator<UpdatePortalAdminBillingSettingsRequest>
{
    public UpdatePortalAdminBillingSettingsRequestValidator()
    {
        RuleFor(x => x.BasicSoftwareFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VesselFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StaffFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.InvoicePrefix).NotEmpty().MaximumLength(20);
        RuleFor(x => x.StartingSequenceNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.DefaultCurrency).NotEmpty().MaximumLength(3).Must(BeSupportedCurrency);
        RuleFor(x => x.DefaultDueDays).InclusiveBetween(1, 120);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BankName).MaximumLength(120);
        RuleFor(x => x.Branch).MaximumLength(120);
        RuleFor(x => x.PaymentInstructions).MaximumLength(600);
        RuleFor(x => x.InvoiceFooterNote).MaximumLength(600);
        RuleFor(x => x.InvoiceTerms).MaximumLength(1200);
        RuleFor(x => x.LogoUrl)
            .MaximumLength(400)
            .Must(BeSupportedLogoUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.LogoUrl))
            .WithMessage("Logo URL must use HTTP/HTTPS, a root static path, or start with /uploads.");
        RuleFor(x => x.EmailFromName).MaximumLength(120);
        RuleFor(x => x.ReplyToEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ReplyToEmail));
    }

    private static bool BeSupportedCurrency(string currency)
        => string.Equals(currency.Trim(), "MVR", StringComparison.OrdinalIgnoreCase)
           || string.Equals(currency.Trim(), "USD", StringComparison.OrdinalIgnoreCase);

    private static bool BeSupportedLogoUrl(string? value)
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

public class PortalAdminBillingInvoiceListQueryValidator : AbstractValidator<PortalAdminBillingInvoiceListQuery>
{
    public PortalAdminBillingInvoiceListQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(160);
        RuleFor(x => x.Currency).MaximumLength(3).Must(BeSupportedCurrency).When(x => !string.IsNullOrWhiteSpace(x.Currency));
    }

    private static bool BeSupportedCurrency(string currency)
        => string.Equals(currency.Trim(), "MVR", StringComparison.OrdinalIgnoreCase)
           || string.Equals(currency.Trim(), "USD", StringComparison.OrdinalIgnoreCase);
}

public class PortalAdminBillingGenerateInvoiceRequestValidator : AbstractValidator<PortalAdminBillingGenerateInvoiceRequest>
{
    public PortalAdminBillingGenerateInvoiceRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BillingMonth).Must(x => x.Day == 1)
            .WithMessage("BillingMonth must be the first day of the month.");
    }
}

public class PortalAdminBillingGenerateBulkInvoicesRequestValidator : AbstractValidator<PortalAdminBillingGenerateBulkInvoicesRequest>
{
    public PortalAdminBillingGenerateBulkInvoicesRequestValidator()
    {
        RuleFor(x => x.BillingMonth).Must(x => x.Day == 1)
            .WithMessage("BillingMonth must be the first day of the month.");
        RuleForEach(x => x.TenantIds).NotEmpty();
    }
}

public class PortalAdminBillingCustomInvoiceRequestValidator : AbstractValidator<PortalAdminBillingCustomInvoiceRequest>
{
    public PortalAdminBillingCustomInvoiceRequestValidator()
    {
        RuleFor(x => x.TenantIds)
            .NotEmpty()
            .WithMessage("At least one business must be selected.");
        RuleForEach(x => x.TenantIds).NotEmpty();
        RuleFor(x => x.BillingMonth).Must(x => x.Day == 1)
            .WithMessage("BillingMonth must be the first day of the month.");
        RuleFor(x => x.Currency).MaximumLength(3).Must(BeSupportedCurrency).When(x => !string.IsNullOrWhiteSpace(x.Currency));
        RuleFor(x => x.SoftwareFee).GreaterThanOrEqualTo(0).When(x => x.SoftwareFee.HasValue);
        RuleFor(x => x.VesselFee).GreaterThanOrEqualTo(0).When(x => x.VesselFee.HasValue);
        RuleFor(x => x.StaffFee).GreaterThanOrEqualTo(0).When(x => x.StaffFee.HasValue);
        RuleFor(x => x.Notes).MaximumLength(700);
        RuleForEach(x => x.LineItems).SetValidator(new PortalAdminBillingCustomInvoiceLineItemRequestValidator());
    }

    private static bool BeSupportedCurrency(string currency)
        => string.Equals(currency.Trim(), "MVR", StringComparison.OrdinalIgnoreCase)
           || string.Equals(currency.Trim(), "USD", StringComparison.OrdinalIgnoreCase);
}

public class PortalAdminBillingCustomInvoiceLineItemRequestValidator : AbstractValidator<PortalAdminBillingCustomInvoiceLineItemRequest>
{
    public PortalAdminBillingCustomInvoiceLineItemRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
    }
}

public class PortalAdminBillingSendInvoiceEmailRequestValidator : AbstractValidator<PortalAdminBillingSendInvoiceEmailRequest>
{
    public PortalAdminBillingSendInvoiceEmailRequestValidator()
    {
        RuleFor(x => x.ToEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ToEmail));
        RuleFor(x => x.CcEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CcEmail));
    }
}

public class PortalAdminBillingCustomRateQueryValidator : AbstractValidator<PortalAdminBillingCustomRateQuery>
{
    public PortalAdminBillingCustomRateQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(160);
    }
}

public class PortalAdminBillingUpsertCustomRateRequestValidator : AbstractValidator<PortalAdminBillingUpsertCustomRateRequest>
{
    public PortalAdminBillingUpsertCustomRateRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.SoftwareFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VesselFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StaffFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(3).Must(BeSupportedCurrency);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x)
            .Must(x => !x.EffectiveFrom.HasValue || !x.EffectiveTo.HasValue || x.EffectiveFrom.Value <= x.EffectiveTo.Value)
            .WithMessage("EffectiveFrom must be before or equal to EffectiveTo.");
    }

    private static bool BeSupportedCurrency(string currency)
        => string.Equals(currency.Trim(), "MVR", StringComparison.OrdinalIgnoreCase)
           || string.Equals(currency.Trim(), "USD", StringComparison.OrdinalIgnoreCase);
}
