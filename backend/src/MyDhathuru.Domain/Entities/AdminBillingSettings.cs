using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class AdminBillingSettings : AuditableEntity
{
    public decimal BasicSoftwareFee { get; set; } = 2500m;
    public decimal VesselFee { get; set; } = 1000m;
    public decimal StaffFee { get; set; } = 250m;
    public string InvoicePrefix { get; set; } = "ADM";
    public int StartingSequenceNumber { get; set; } = 1;
    public string DefaultCurrency { get; set; } = "MVR";
    public int DefaultDueDays { get; set; } = 14;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? Branch { get; set; }
    public string? PaymentInstructions { get; set; }
    public string? InvoiceFooterNote { get; set; }
    public string? InvoiceTerms { get; set; }
    public string? LogoUrl { get; set; }
    public string? EmailFromName { get; set; }
    public string? ReplyToEmail { get; set; }
    public bool AutoGenerationEnabled { get; set; }
    public bool AutoEmailEnabled { get; set; }
}
