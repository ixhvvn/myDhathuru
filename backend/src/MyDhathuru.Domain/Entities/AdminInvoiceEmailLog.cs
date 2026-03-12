using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class AdminInvoiceEmailLog : AuditableEntity
{
    public Guid AdminInvoiceId { get; set; }
    public AdminInvoice AdminInvoice { get; set; } = null!;
    public string ToEmail { get; set; } = string.Empty;
    public string? CcEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
    public AdminInvoiceEmailStatus Status { get; set; } = AdminInvoiceEmailStatus.Sent;
    public string? ErrorMessage { get; set; }
    public Guid? AttemptedByUserId { get; set; }
}
