using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class TenantSettings : TenantEntity
{
    public string Username { get; set; } = string.Empty;
    public required string CompanyName { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhone { get; set; }
    public required string TinNumber { get; set; }
    public required string BusinessRegistrationNumber { get; set; }
    public string InvoicePrefix { get; set; } = "INV";
    public string DeliveryNotePrefix { get; set; } = "DN";
    public string StatementPrefix { get; set; } = "ST";
    public string SalarySlipPrefix { get; set; } = "SLIP";
    public decimal DefaultTaxRate { get; set; } = 0.08m;
    public int DefaultDueDays { get; set; } = 7;
    public string DefaultCurrency { get; set; } = "MVR";
    public string BmlMvrAccountName { get; set; } = string.Empty;
    public string BmlMvrAccountNumber { get; set; } = string.Empty;
    public string BmlUsdAccountName { get; set; } = string.Empty;
    public string BmlUsdAccountNumber { get; set; } = string.Empty;
    public string MibMvrAccountName { get; set; } = string.Empty;
    public string MibMvrAccountNumber { get; set; } = string.Empty;
    public string MibUsdAccountName { get; set; } = string.Empty;
    public string MibUsdAccountNumber { get; set; } = string.Empty;
    public string InvoiceOwnerName { get; set; } = string.Empty;
    public string InvoiceOwnerIdCard { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
