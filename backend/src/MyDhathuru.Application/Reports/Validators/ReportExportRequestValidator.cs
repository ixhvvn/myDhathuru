using FluentValidation;
using MyDhathuru.Application.Reports.Dtos;

namespace MyDhathuru.Application.Reports.Validators;

public class ReportExportRequestValidator : AbstractValidator<ReportExportRequest>
{
    private const int MaxCustomRangeMonths = 6;

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
            .WithMessage("Custom range must have valid start/end dates and cannot exceed six months.");
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

        return request.CustomEndDate.Value <= request.CustomStartDate.Value.AddMonths(MaxCustomRangeMonths);
    }
}
