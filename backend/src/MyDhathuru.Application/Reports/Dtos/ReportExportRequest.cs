namespace MyDhathuru.Application.Reports.Dtos;

public class ReportExportRequest
{
    public ReportType ReportType { get; set; }
    public ReportDatePreset DatePreset { get; set; } = ReportDatePreset.Today;
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
    public Guid? CustomerId { get; set; }
}
