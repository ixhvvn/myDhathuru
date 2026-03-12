using FluentValidation;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Reports.Validators;

public class ReportExportRequestValidator : AbstractValidator<ReportExportRequest>
{
    public ReportExportRequestValidator()
    {
        RuleFor(x => x.ReportType)
            .IsInEnum()
            .WithMessage("Report type is invalid.");

        RuleFor(x => x.DatePreset)
            .IsInEnum()
            .WithMessage("Date preset is invalid.");

        RuleFor(x => x)
            .Must(IsValidCustomRange)
            .WithMessage("Custom range must have valid start/end dates and cannot exceed 31 days.");
    }

    private static bool IsValidCustomRange(ReportExportRequest request)
    {
        if (request.DatePreset != ReportDatePreset.CustomRange)
        {
            return true;
        }

        if (!request.CustomStartDate.HasValue || !request.CustomEndDate.HasValue)
        {
            return false;
        }

        if (request.CustomEndDate.Value < request.CustomStartDate.Value)
        {
            return false;
        }

        var rangeDays = request.CustomEndDate.Value.DayNumber - request.CustomStartDate.Value.DayNumber + 1;
        return rangeDays <= 31;
    }
}
