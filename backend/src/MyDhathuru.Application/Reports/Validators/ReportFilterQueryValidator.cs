using FluentValidation;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Reports.Validators;

public class ReportFilterQueryValidator : AbstractValidator<ReportFilterQuery>
{
    public ReportFilterQueryValidator()
    {
        RuleFor(x => x.DatePreset)
            .IsInEnum()
            .WithMessage("Date preset is invalid.");

        RuleFor(x => x)
            .Must(IsValidCustomRange)
            .WithMessage("Custom range must have valid start/end dates and cannot exceed 31 days.");
    }

    private static bool IsValidCustomRange(ReportFilterQuery query)
    {
        if (query.DatePreset != ReportDatePreset.CustomRange)
        {
            return true;
        }

        if (!query.CustomStartDate.HasValue || !query.CustomEndDate.HasValue)
        {
            return false;
        }

        if (query.CustomEndDate.Value < query.CustomStartDate.Value)
        {
            return false;
        }

        var rangeDays = query.CustomEndDate.Value.DayNumber - query.CustomStartDate.Value.DayNumber + 1;
        return rangeDays <= 31;
    }
}
