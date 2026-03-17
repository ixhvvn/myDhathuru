using FluentValidation;
using MyDhathuru.Application.StaffConduct.Dtos;

namespace MyDhathuru.Application.StaffConduct.Validators;

public class CreateStaffConductFormRequestValidator : AbstractValidator<CreateStaffConductFormRequest>
{
    public CreateStaffConductFormRequestValidator()
    {
        RuleFor(x => x.StaffId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IncidentDetails).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ActionTaken).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RequiredImprovement).MaximumLength(1000);
        RuleFor(x => x.IssuedBy).NotEmpty().MaximumLength(150);
        RuleFor(x => x.WitnessedBy).MaximumLength(150);
        RuleFor(x => x.EmployeeRemarks).MaximumLength(1000);
        RuleFor(x => x.ResolutionNotes).MaximumLength(1000);
        RuleFor(x => x.IncidentDate).LessThanOrEqualTo(x => x.IssueDate);
        RuleFor(x => x.AcknowledgedDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.AcknowledgedDate.HasValue);
        RuleFor(x => x.FollowUpDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.FollowUpDate.HasValue);
        RuleFor(x => x.ResolvedDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.ResolvedDate.HasValue);
    }
}

public class StaffConductListQueryValidator : AbstractValidator<StaffConductListQuery>
{
    public StaffConductListQueryValidator()
    {
        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom!.Value)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue);
    }
}

public class UpsertStaffConductDhivehiExportRequestValidator : AbstractValidator<UpsertStaffConductDhivehiExportRequest>
{
    public UpsertStaffConductDhivehiExportRequestValidator()
    {
        RuleFor(x => x.SubjectDv).MaximumLength(200);
        RuleFor(x => x.IncidentDetailsDv).MaximumLength(2000);
        RuleFor(x => x.ActionTakenDv).MaximumLength(1000);
        RuleFor(x => x.RequiredImprovementDv).MaximumLength(1000);
        RuleFor(x => x.EmployeeRemarksDv).MaximumLength(1000);
        RuleFor(x => x.AcknowledgementDv).MaximumLength(1000);
        RuleFor(x => x.ResolutionNotesDv).MaximumLength(1000);
    }
}
