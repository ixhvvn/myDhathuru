using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.StaffConduct.Dtos;

public class StaffConductListQuery : PaginationQuery
{
    public Guid? StaffId { get; set; }
    public StaffConductFormType? FormType { get; set; }
    public StaffConductStatus? Status { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

public class StaffConductSummaryDto
{
    public int TotalForms { get; set; }
    public int WarningCount { get; set; }
    public int DisciplinaryCount { get; set; }
    public int OpenCount { get; set; }
    public int AcknowledgedCount { get; set; }
    public int ResolvedCount { get; set; }
}

public class StaffConductStaffOptionDto
{
    public Guid Id { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
}

public class StaffConductListItemDto
{
    public Guid Id { get; set; }
    public string FormNumber { get; set; } = string.Empty;
    public StaffConductFormType FormType { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly IncidentDate { get; set; }
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
    public string Subject { get; set; } = string.Empty;
    public StaffConductSeverity Severity { get; set; }
    public StaffConductStatus Status { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsAcknowledgedByStaff { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public DateOnly? ResolvedDate { get; set; }
}

public class StaffConductDetailDto : StaffConductListItemDto
{
    public string? IdNumber { get; set; }
    public string IncidentDetails { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string? RequiredImprovement { get; set; }
    public string? WitnessedBy { get; set; }
    public DateOnly? AcknowledgedDate { get; set; }
    public string? EmployeeRemarks { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class CreateStaffConductFormRequest
{
    public Guid StaffId { get; set; }
    public StaffConductFormType FormType { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly IncidentDate { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string IncidentDetails { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string? RequiredImprovement { get; set; }
    public StaffConductSeverity Severity { get; set; }
    public StaffConductStatus Status { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public string? WitnessedBy { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public bool IsAcknowledgedByStaff { get; set; }
    public DateOnly? AcknowledgedDate { get; set; }
    public string? EmployeeRemarks { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateOnly? ResolvedDate { get; set; }
}

public class UpdateStaffConductFormRequest : CreateStaffConductFormRequest
{
}
