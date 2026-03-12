using FluentValidation;
using MyDhathuru.Application.Support.Dtos;

namespace MyDhathuru.Application.Support.Validators;

public class ReportBugRequestValidator : AbstractValidator<ReportBugRequest>
{
    public ReportBugRequestValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(3000);

        RuleFor(x => x.PageUrl)
            .MaximumLength(500)
            .Must(BeAValidAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.PageUrl))
            .WithMessage("Page URL must be a valid absolute URL.");
    }

    private static bool BeAValidAbsoluteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }
}
