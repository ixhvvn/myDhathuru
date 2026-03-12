namespace MyDhathuru.Application.Reports.Dtos;

public class ReportMetaDto
{
    public ReportDatePreset DatePreset { get; set; }
    public DateTimeOffset RangeStartUtc { get; set; }
    public DateTimeOffset RangeEndUtc { get; set; }
    public string CustomerFilterLabel { get; set; } = "All Customers";
    public DateTimeOffset GeneratedAtUtc { get; set; }
}
