using MyDhathuru.Application.Support.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendPasswordResetAsync(string email, string token, bool isPortalAdmin, CancellationToken cancellationToken = default);
    Task SendSignupAcceptedAsync(string email, string companyName, CancellationToken cancellationToken = default);
    Task SendSignupRejectedAsync(string email, string companyName, string rejectionReason, CancellationToken cancellationToken = default);
    Task SendPortalAdminInvoiceAsync(
        string toEmail,
        string? ccEmail,
        string companyName,
        DateOnly billingMonth,
        string subject,
        byte[] pdfBytes,
        string attachmentFileName,
        string? fromName,
        string? replyToEmail,
        CancellationToken cancellationToken = default);
    Task SendDocumentEmailAsync(
        string toEmail,
        string? ccEmail,
        string subject,
        string body,
        byte[] pdfBytes,
        string attachmentFileName,
        string? fromName,
        string? replyToEmail,
        CancellationToken cancellationToken = default);
    Task SendPortalAdminAnnouncementAsync(
        string toEmail,
        IReadOnlyCollection<string> ccEmails,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
    Task SendBugReportAsync(
        string reporterName,
        string? reporterEmail,
        string? companyName,
        string subject,
        string description,
        string? pageUrl,
        BugReportAttachment? attachment,
        CancellationToken cancellationToken = default);
}
