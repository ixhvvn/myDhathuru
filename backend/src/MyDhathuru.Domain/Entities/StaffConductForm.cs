using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class StaffConductForm : TenantEntity
{
    public Guid StaffId { get; set; }
    public Staff Staff { get; set; } = null!;

    public string FormNumber { get; set; } = string.Empty;
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
    public string? SubjectDv { get; set; }
    public string? IncidentDetailsDv { get; set; }
    public string? ActionTakenDv { get; set; }
    public string? RequiredImprovementDv { get; set; }
    public string? EmployeeRemarksDv { get; set; }
    public string? AcknowledgementDv { get; set; }
    public string? ResolutionNotesDv { get; set; }

    public string StaffCodeSnapshot { get; set; } = string.Empty;
    public string StaffNameSnapshot { get; set; } = string.Empty;
    public string? DesignationSnapshot { get; set; }
    public string? WorkSiteSnapshot { get; set; }
    public string? IdNumberSnapshot { get; set; }

    public ICollection<StaffConductExportDocument> ExportDocuments { get; set; } = new List<StaffConductExportDocument>();
}
