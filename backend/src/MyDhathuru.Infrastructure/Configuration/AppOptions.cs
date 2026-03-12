namespace MyDhathuru.Infrastructure.Configuration;

public class AppOptions
{
    public const string SectionName = "App";

    public string FrontendBaseUrl { get; set; } = "http://localhost:81";
    public string AdminFrontendBaseUrl { get; set; } = "http://localhost:81";
    public string BugReportRecipient { get; set; } = "mydhathuru@gmail.com";
}
