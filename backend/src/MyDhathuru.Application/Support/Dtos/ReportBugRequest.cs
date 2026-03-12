namespace MyDhathuru.Application.Support.Dtos;

public class ReportBugRequest
{
    public required string Subject { get; set; }
    public required string Description { get; set; }
    public string? PageUrl { get; set; }
}

public class BugReportAttachment
{
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Content { get; set; }
}
