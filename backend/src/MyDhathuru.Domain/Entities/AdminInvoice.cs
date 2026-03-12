using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Domain.Entities;

public class AdminInvoice : AuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateOnly BillingMonth { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public string Currency { get; set; } = "MVR";

    public string CompanyNameSnapshot { get; set; } = string.Empty;
    public string CompanyEmailSnapshot { get; set; } = string.Empty;
    public string CompanyPhoneSnapshot { get; set; } = string.Empty;
    public string CompanyTinSnapshot { get; set; } = string.Empty;
    public string CompanyRegistrationSnapshot { get; set; } = string.Empty;
    public string? CompanyAdminNameSnapshot { get; set; }
    public string? CompanyAdminEmailSnapshot { get; set; }

    public decimal BaseSoftwareFee { get; set; }
    public int VesselCount { get; set; }
    public decimal VesselRate { get; set; }
    public decimal VesselAmount { get; set; }
    public int StaffCount { get; set; }
    public decimal StaffRate { get; set; }
    public decimal StaffAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public AdminInvoiceStatus Status { get; set; } = AdminInvoiceStatus.Issued;
    public bool IsCustom { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public Guid? CustomRateId { get; set; }
    public BusinessCustomRate? CustomRate { get; set; }

    public ICollection<AdminInvoiceLineItem> LineItems { get; set; } = new List<AdminInvoiceLineItem>();
    public ICollection<AdminInvoiceEmailLog> EmailLogs { get; set; } = new List<AdminInvoiceEmailLog>();
}
