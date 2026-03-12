namespace MyDhathuru.Application.Settings.Dtos;

public class TenantSettingsDto
{
    public string Username { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string TinNumber { get; set; } = string.Empty;
    public string BusinessRegistrationNumber { get; set; } = string.Empty;
    public string InvoicePrefix { get; set; } = string.Empty;
    public string DeliveryNotePrefix { get; set; } = string.Empty;
    public decimal DefaultTaxRate { get; set; }
    public int DefaultDueDays { get; set; }
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
}

public class UpdateTenantSettingsRequest
{
    public string Username { get; set; } = string.Empty;
    public required string CompanyName { get; set; }
    public required string CompanyEmail { get; set; }
    public required string CompanyPhone { get; set; }
    public required string TinNumber { get; set; }
    public required string BusinessRegistrationNumber { get; set; }
    public required string InvoicePrefix { get; set; }
    public required string DeliveryNotePrefix { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public int DefaultDueDays { get; set; }
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
}

public class TenantLogoUploadDto
{
    public string Url { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
    public required string ConfirmPassword { get; set; }
}
