namespace MyDhathuru.Application.Reports.Dtos;

public class ReportFilterQuery
{
    public ReportDatePreset DatePreset { get; set; } = ReportDatePreset.Today;
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
    public Guid? CustomerId { get; set; }
}
