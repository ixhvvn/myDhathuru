using FluentValidation;
using MyDhathuru.Application.Bpt.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Bpt.Validators;

public class BptReportQueryValidator : AbstractValidator<BptReportQuery>
{
    public BptReportQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Quarter)
            .InclusiveBetween(1, 4)
            .When(x => x.PeriodMode == BptPeriodMode.Quarter && x.Quarter.HasValue);
        RuleFor(x => x)
            .Must(HaveValidCustomRange)
            .WithMessage("Custom range requires both start and end dates, and end date cannot be earlier than start date.")
            .When(x => x.PeriodMode == BptPeriodMode.CustomRange);
    }

    private static bool HaveValidCustomRange(BptReportQuery query)
    {
        return query.CustomStartDate.HasValue
            && query.CustomEndDate.HasValue
            && query.CustomEndDate.Value >= query.CustomStartDate.Value;
    }
}

public class BptReportExportRequestValidator : AbstractValidator<BptReportExportRequest>
{
    public BptReportExportRequestValidator()
    {
        Include(new BptReportQueryValidator());
    }
}

public class UpsertBptExpenseMappingRequestValidator : AbstractValidator<UpsertBptExpenseMappingRequest>
{
    public UpsertBptExpenseMappingRequestValidator()
    {
        RuleFor(x => x.BptCategoryId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(600);
    }
}

public class BptExchangeRateListQueryValidator : AbstractValidator<BptExchangeRateListQuery>
{
    public BptExchangeRateListQueryValidator()
    {
        RuleFor(x => x.Currency)
            .Must(BptValidatorHelpers.BeValidCurrency)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("End date cannot be earlier than start date.");
    }

    private static bool HaveValidRange(BptExchangeRateListQuery query)
        => !query.DateFrom.HasValue || !query.DateTo.HasValue || query.DateTo.Value >= query.DateFrom.Value;
}

public class UpsertBptExchangeRateRequestValidator : AbstractValidator<UpsertBptExchangeRateRequest>
{
    public UpsertBptExchangeRateRequestValidator()
    {
        RuleFor(x => x.RateDate).NotEmpty();
        RuleFor(x => x.Currency)
            .Must(BptValidatorHelpers.BeValidCurrency)
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.RateToMvr).GreaterThan(0);
        RuleFor(x => x.Source).MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(600);
    }
}

public class SalesAdjustmentListQueryValidator : AbstractValidator<SalesAdjustmentListQuery>
{
    public SalesAdjustmentListQueryValidator()
    {
        RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("End date cannot be earlier than start date.");
    }

    private static bool HaveValidRange(SalesAdjustmentListQuery query)
        => !query.DateFrom.HasValue || !query.DateTo.HasValue || query.DateTo.Value >= query.DateFrom.Value;
}

public class CreateSalesAdjustmentRequestValidator : AbstractValidator<CreateSalesAdjustmentRequest>
{
    public CreateSalesAdjustmentRequestValidator()
    {
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.AmountOriginal).NotEqual(0);
        RuleFor(x => x.Currency)
            .Must(BptValidatorHelpers.BeValidCurrency)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0)
            .When(x => string.Equals(BptValidatorHelpers.NormalizeCurrency(x.Currency), CurrencyCode.USD.ToString(), StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class UpdateSalesAdjustmentRequestValidator : AbstractValidator<UpdateSalesAdjustmentRequest>
{
    public UpdateSalesAdjustmentRequestValidator()
    {
        Include(new CreateSalesAdjustmentRequestValidator());
    }
}

public class OtherIncomeEntryListQueryValidator : AbstractValidator<OtherIncomeEntryListQuery>
{
    public OtherIncomeEntryListQueryValidator()
    {
        RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("End date cannot be earlier than start date.");
    }

    private static bool HaveValidRange(OtherIncomeEntryListQuery query)
        => !query.DateFrom.HasValue || !query.DateTo.HasValue || query.DateTo.Value >= query.DateFrom.Value;
}

public class CreateOtherIncomeEntryRequestValidator : AbstractValidator<CreateOtherIncomeEntryRequest>
{
    public CreateOtherIncomeEntryRequestValidator()
    {
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CounterpartyName).MaximumLength(200);
        RuleFor(x => x.AmountOriginal).NotEqual(0);
        RuleFor(x => x.Currency)
            .Must(BptValidatorHelpers.BeValidCurrency)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0)
            .When(x => string.Equals(BptValidatorHelpers.NormalizeCurrency(x.Currency), CurrencyCode.USD.ToString(), StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class UpdateOtherIncomeEntryRequestValidator : AbstractValidator<UpdateOtherIncomeEntryRequest>
{
    public UpdateOtherIncomeEntryRequestValidator()
    {
        Include(new CreateOtherIncomeEntryRequestValidator());
    }
}

public class BptAdjustmentListQueryValidator : AbstractValidator<BptAdjustmentListQuery>
{
    public BptAdjustmentListQueryValidator()
    {
        RuleFor(x => x)
            .Must(HaveValidRange)
            .WithMessage("End date cannot be earlier than start date.");
    }

    private static bool HaveValidRange(BptAdjustmentListQuery query)
        => !query.DateFrom.HasValue || !query.DateTo.HasValue || query.DateTo.Value >= query.DateFrom.Value;
}

public class CreateBptAdjustmentRequestValidator : AbstractValidator<CreateBptAdjustmentRequest>
{
    public CreateBptAdjustmentRequestValidator()
    {
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.BptCategoryId).NotEmpty();
        RuleFor(x => x.AmountOriginal).NotEqual(0);
        RuleFor(x => x.Currency)
            .Must(BptValidatorHelpers.BeValidCurrency)
            .When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("Currency must be MVR or USD.");
        RuleFor(x => x.ExchangeRate)
            .GreaterThan(0)
            .When(x => string.Equals(BptValidatorHelpers.NormalizeCurrency(x.Currency), CurrencyCode.USD.ToString(), StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public class UpdateBptAdjustmentRequestValidator : AbstractValidator<UpdateBptAdjustmentRequest>
{
    public UpdateBptAdjustmentRequestValidator()
    {
        Include(new CreateBptAdjustmentRequestValidator());
    }
}

internal static class BptValidatorHelpers
{
    public static bool BeValidCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return false;
        }

        return Enum.TryParse<CurrencyCode>(currency.Trim(), true, out _);
    }

    public static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? CurrencyCode.MVR.ToString() : currency.Trim().ToUpperInvariant();
}
