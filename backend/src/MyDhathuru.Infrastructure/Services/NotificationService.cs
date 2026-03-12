using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyDhathuru.Application.Common.Exceptions;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Support.Dtos;
using MyDhathuru.Infrastructure.Configuration;

namespace MyDhathuru.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly SmtpOptions _smtpOptions;
    private readonly AppOptions _appOptions;

    public NotificationService(
        ILogger<NotificationService> logger,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<AppOptions> appOptions)
    {
        _logger = logger;
        _smtpOptions = smtpOptions.Value;
        _appOptions = appOptions.Value;
    }

    public async Task SendPasswordResetAsync(string email, string token, bool isPortalAdmin, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValidSmtpConfiguration())
        {
            _logger.LogWarning(
                "SMTP configuration is incomplete. Password reset token for {Email}: {Token}",
                email,
                token);
            throw new AppException("Password reset email is not configured.");
        }

        var body = BuildResetMessage(email, token, isPortalAdmin);
        await SendAsync(email, "myDhathuru Password Reset", body, cancellationToken);
    }

    public async Task SendSignupAcceptedAsync(string email, string companyName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValidSmtpConfiguration())
        {
            _logger.LogWarning("SMTP configuration is incomplete. Signup accepted email cannot be sent.");
            throw new AppException("Signup notification email is not configured.");
        }

        var loginUrl = $"{_appOptions.FrontendBaseUrl.TrimEnd('/')}/login";
        var body = BuildFormalEmail(new[]
        {
            $"Your signup request for <strong>{HtmlEncoder.Default.Encode(companyName)}</strong> has been approved.",
            $"You may now log in to the portal using your admin credentials: <a href=\"{loginUrl}\">{loginUrl}</a>."
        });

        await SendAsync(email, "myDhathuru Signup Approved", body, cancellationToken);
    }

    public async Task SendSignupRejectedAsync(string email, string companyName, string rejectionReason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValidSmtpConfiguration())
        {
            _logger.LogWarning("SMTP configuration is incomplete. Signup rejected email cannot be sent.");
            throw new AppException("Signup notification email is not configured.");
        }

        var body = BuildFormalEmail(new[]
        {
            $"Your signup request for <strong>{HtmlEncoder.Default.Encode(companyName)}</strong> was not approved.",
            $"Reason: <strong>{HtmlEncoder.Default.Encode(rejectionReason)}</strong>."
        });

        await SendAsync(email, "myDhathuru Signup Rejected", body, cancellationToken);
    }

    public async Task SendPortalAdminInvoiceAsync(
        string toEmail,
        string? ccEmail,
        string companyName,
        DateOnly billingMonth,
        string subject,
        byte[] pdfBytes,
        string attachmentFileName,
        string? fromName,
        string? replyToEmail,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValidSmtpConfiguration())
        {
            _logger.LogWarning("SMTP configuration is incomplete. Portal admin invoice email cannot be sent.");
            throw new AppException("Invoice email is not configured.");
        }

        if (pdfBytes.Length == 0)
        {
            throw new AppException("Invoice attachment is empty.");
        }

        var body = BuildPortalAdminInvoiceBody(billingMonth);
        await SendWithAttachmentAsync(
            toEmail,
            ccEmail,
            subject,
            body,
            pdfBytes,
            attachmentFileName,
            fromName,
            replyToEmail,
            cancellationToken);

        _logger.LogInformation(
            "Portal admin invoice email sent to {ToEmail} (cc: {CcEmail}) for {CompanyName} and month {BillingMonth}",
            toEmail,
            ccEmail,
            companyName,
            billingMonth.ToString("yyyy-MM"));
    }

    public async Task SendBugReportAsync(
        string reporterName,
        string? reporterEmail,
        string? companyName,
        string subject,
        string description,
        string? pageUrl,
        BugReportAttachment? attachment,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HasValidSmtpConfiguration())
        {
            _logger.LogWarning("SMTP configuration is incomplete. Bug report cannot be sent.");
            throw new AppException("Unable to send bug report at the moment.");
        }

        var fromEmail = string.IsNullOrWhiteSpace(_smtpOptions.FromEmail)
            ? _smtpOptions.Username
            : _smtpOptions.FromEmail;

        var recipient = string.IsNullOrWhiteSpace(_appOptions.BugReportRecipient)
            ? "mydhathuru@gmail.com"
            : _appOptions.BugReportRecipient.Trim();

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, _smtpOptions.FromName),
                Subject = $"myDhathuru Bug Report: {subject.Trim()}",
                Body = BuildBugReportMessage(
                    reporterName,
                    reporterEmail,
                    companyName,
                    subject,
                    description,
                    pageUrl,
                    attachment?.FileName),
                IsBodyHtml = true
            };
            message.To.Add(recipient);

            if (attachment is not null)
            {
                var stream = new MemoryStream(attachment.Content);
                message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            using var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                EnableSsl = _smtpOptions.UseSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation("Bug report email sent by {ReporterEmail} to {Recipient}", reporterEmail, recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bug report email by {ReporterEmail}", reporterEmail);
            throw new AppException("Unable to send bug report at the moment.");
        }
    }

    private bool HasValidSmtpConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_smtpOptions.Host)
            && _smtpOptions.Port > 0
            && !string.IsNullOrWhiteSpace(_smtpOptions.Username)
            && !string.IsNullOrWhiteSpace(_smtpOptions.Password);
    }

    private string BuildResetMessage(string email, string token, bool isPortalAdmin)
    {
        var frontendBase = isPortalAdmin && !string.IsNullOrWhiteSpace(_appOptions.AdminFrontendBaseUrl)
            ? _appOptions.AdminFrontendBaseUrl
            : _appOptions.FrontendBaseUrl;
        var frontend = frontendBase.TrimEnd('/');
        var resetPath = isPortalAdmin ? "/portal-admin/reset-password" : "/reset-password";
        var resetUrl = $"{frontend}{resetPath}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        return BuildFormalEmail(new[]
        {
            "We received a request to reset your password.",
            $"Use this secure link to continue: <a href=\"{resetUrl}\">{resetUrl}</a>.",
            "This reset link expires in 30 minutes.",
            "If you did not request this password reset, you can safely ignore this email."
        });
    }

    private async Task SendAsync(string recipientEmail, string subject, string bodyHtml, CancellationToken cancellationToken)
    {
        var fromEmail = string.IsNullOrWhiteSpace(_smtpOptions.FromEmail)
            ? _smtpOptions.Username
            : _smtpOptions.FromEmail;

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, _smtpOptions.FromName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            message.To.Add(recipientEmail);

            using var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                EnableSsl = _smtpOptions.UseSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await smtpClient.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email with subject {Subject} to {Email}", subject, recipientEmail);
            throw new AppException("Unable to send email.");
        }
    }

    private async Task SendWithAttachmentAsync(
        string toEmail,
        string? ccEmail,
        string subject,
        string bodyHtml,
        byte[] attachmentBytes,
        string attachmentFileName,
        string? fromName,
        string? replyToEmail,
        CancellationToken cancellationToken)
    {
        var fromEmail = string.IsNullOrWhiteSpace(_smtpOptions.FromEmail)
            ? _smtpOptions.Username
            : _smtpOptions.FromEmail;
        var senderName = string.IsNullOrWhiteSpace(fromName)
            ? _smtpOptions.FromName
            : fromName.Trim();

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, senderName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);
            if (!string.IsNullOrWhiteSpace(ccEmail))
            {
                message.CC.Add(ccEmail.Trim());
            }

            if (!string.IsNullOrWhiteSpace(replyToEmail))
            {
                message.ReplyToList.Add(new MailAddress(replyToEmail.Trim()));
            }

            using var stream = new MemoryStream(attachmentBytes);
            var attachment = new Attachment(stream, attachmentFileName, MediaTypeNames.Application.Pdf);
            message.Attachments.Add(attachment);

            using var smtpClient = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
            {
                EnableSsl = _smtpOptions.UseSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpOptions.Username, _smtpOptions.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await smtpClient.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invoice email with subject {Subject} to {Email}", subject, toEmail);
            throw new AppException("Unable to send invoice email.");
        }
    }

    private static string BuildFormalEmail(IEnumerable<string> bodyLines)
    {
        var paragraphs = string.Join(
            string.Empty,
            bodyLines.Select(line => $@"<p style=""margin:0 0 12px;color:#42557f;line-height:1.55;"">{line}</p>"));

        return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f8ff;padding:24px;"">
  <div style=""max-width:640px;margin:0 auto;background:#ffffff;border:1px solid #d9e4fb;border-radius:14px;padding:24px;"">
    <p style=""margin:0 0 14px;color:#223b6c;line-height:1.55;""><strong>Dear Sir/Madam,</strong></p>
    {paragraphs}
    <p style=""margin:14px 0 0;color:#223b6c;line-height:1.55;"">Best regards,</p>
    <p style=""margin:4px 0 0;color:#4d6290;"">myDhathuru Team</p>
  </div>
</div>";
    }

    private static string BuildPortalAdminInvoiceBody(DateOnly billingMonth)
    {
        var monthText = billingMonth.ToString("MMMM yyyy");
        return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f8ff;padding:24px;"">
  <div style=""max-width:640px;margin:0 auto;background:#ffffff;border:1px solid #d9e4fb;border-radius:14px;padding:24px;"">
    <p style=""margin:0 0 14px;color:#223b6c;line-height:1.55;""><strong>Dear Team,</strong></p>
    <p style=""margin:0 0 12px;color:#42557f;line-height:1.55;"">
      Please find attached the invoice for {monthText}, which covers the fee issued in advance.
      Kindly process the payment at your earliest convenience.
    </p>
    <p style=""margin:14px 0 0;color:#223b6c;line-height:1.55;"">Regards,</p>
    <p style=""margin:4px 0 0;color:#4d6290;"">myDhathuru Team</p>
  </div>
</div>";
    }

    private static string BuildBugReportMessage(
        string reporterName,
        string? reporterEmail,
        string? companyName,
        string subject,
        string description,
        string? pageUrl,
        string? attachmentFileName)
    {
        var encoder = HtmlEncoder.Default;
        var encodedSubject = encoder.Encode(subject.Trim());
        var encodedDescription = encoder
            .Encode(description.Trim())
            .Replace("\r\n", "<br />")
            .Replace("\n", "<br />");
        var encodedReporterName = encoder.Encode(reporterName);
        var encodedReporterEmail = encoder.Encode(reporterEmail ?? "N/A");
        var encodedCompanyName = encoder.Encode(companyName ?? "N/A");
        var encodedPageUrl = encoder.Encode(pageUrl ?? "Not provided");
        var encodedAttachment = encoder.Encode(attachmentFileName ?? "None");

        return $@"
<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f5f8ff;padding:28px;"">
  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d9e4fb;border-radius:14px;padding:24px;"">
    <h2 style=""margin:0 0 12px;color:#243a67;"">New myDhathuru bug report</h2>
    <table style=""width:100%;border-collapse:collapse;margin-bottom:16px;font-size:14px;color:#3f547d;"">
      <tr><td style=""padding:6px 0;font-weight:600;width:140px;"">Subject</td><td style=""padding:6px 0;"">{encodedSubject}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Reported By</td><td style=""padding:6px 0;"">{encodedReporterName}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Reporter Email</td><td style=""padding:6px 0;"">{encodedReporterEmail}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Company</td><td style=""padding:6px 0;"">{encodedCompanyName}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Page URL</td><td style=""padding:6px 0;"">{encodedPageUrl}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Attachment</td><td style=""padding:6px 0;"">{encodedAttachment}</td></tr>
      <tr><td style=""padding:6px 0;font-weight:600;"">Reported At</td><td style=""padding:6px 0;"">{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
    </table>
    <div style=""border:1px solid #dce6fb;border-radius:12px;padding:14px;background:#f9fbff;color:#2f4065;line-height:1.6;"">
      {encodedDescription}
    </div>
  </div>
</div>";
    }
}
