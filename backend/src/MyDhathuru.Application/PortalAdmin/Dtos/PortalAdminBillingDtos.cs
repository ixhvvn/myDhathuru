using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.PortalAdmin.Dtos;

public class PortalAdminBillingDashboardDto
{
    public int InvoicesGeneratedThisMonth { get; set; }
    public decimal TotalBilledMvrThisMonth { get; set; }
    public decimal TotalBilledUsdThisMonth { get; set; }
    public int TotalEmailedThisMonth { get; set; }
    public int PendingEmailCount { get; set; }
    public IReadOnlyList<PortalAdminBillingInvoiceListItemDto> RecentInvoices { get; set; } = Array.Empty<PortalAdminBillingInvoiceListItemDto>();
}

public class PortalAdminBillingStatementTotalsDto
{
    public decimal Mvr { get; set; }
    public decimal Usd { get; set; }
}

public class PortalAdminBillingYearlyStatementMonthDto
{
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public int InvoiceCount { get; set; }
    public decimal TotalMvr { get; set; }
    public decimal TotalUsd { get; set; }
    public int EmailedCount { get; set; }
    public int PendingCount { get; set; }
}

public class PortalAdminBillingYearlyStatementDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int TotalInvoices { get; set; }
    public int EmailedInvoices { get; set; }
    public int PendingInvoices { get; set; }
    public PortalAdminBillingStatementTotalsDto TotalInvoiced { get; set; } = new();
    public IReadOnlyList<PortalAdminBillingYearlyStatementMonthDto> Months { get; set; } = Array.Empty<PortalAdminBillingYearlyStatementMonthDto>();
    public IReadOnlyList<PortalAdminBillingInvoiceListItemDto> Invoices { get; set; } = Array.Empty<PortalAdminBillingInvoiceListItemDto>();
}

public class PortalAdminBillingSettingsDto
{
    public decimal BasicSoftwareFee { get; set; }
    public decimal VesselFee { get; set; }
    public decimal StaffFee { get; set; }
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

public class PortalAdminBillingLogoUploadDto
{
    public string Url { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class UpdatePortalAdminBillingSettingsRequest
{
    public decimal BasicSoftwareFee { get; set; }
    public decimal VesselFee { get; set; }
    public decimal StaffFee { get; set; }
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

public class PortalAdminBillingInvoiceListQuery
{
    public string? Search { get; set; }
    public Guid? TenantId { get; set; }
    public DateOnly? BillingMonth { get; set; }
    public AdminInvoiceStatus? Status { get; set; }
    public string? Currency { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminBillingInvoiceListItemDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Currency { get; set; } = "MVR";
    public AdminInvoiceStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public bool IsCustom { get; set; }
}

public class PortalAdminBillingInvoiceDetailDto : PortalAdminBillingInvoiceListItemDto
{
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyTinNumber { get; set; } = string.Empty;
    public string CompanyRegistrationNumber { get; set; } = string.Empty;
    public string? CompanyAdminName { get; set; }
    public string? CompanyAdminEmail { get; set; }
    public decimal BaseSoftwareFee { get; set; }
    public int VesselCount { get; set; }
    public decimal VesselRate { get; set; }
    public decimal VesselAmount { get; set; }
    public int StaffCount { get; set; }
    public decimal StaffRate { get; set; }
    public decimal StaffAmount { get; set; }
    public decimal Subtotal { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PortalAdminBillingInvoiceLineItemDto> LineItems { get; set; } = Array.Empty<PortalAdminBillingInvoiceLineItemDto>();
    public IReadOnlyList<PortalAdminBillingInvoiceEmailLogDto> EmailLogs { get; set; } = Array.Empty<PortalAdminBillingInvoiceEmailLogDto>();
}

public class PortalAdminBillingInvoiceLineItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public class PortalAdminBillingInvoiceEmailLogDto
{
    public Guid Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? CcEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTimeOffset AttemptedAt { get; set; }
    public AdminInvoiceEmailStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PortalAdminBillingSendInvoiceEmailRequest
{
    public string? ToEmail { get; set; }
    public string? CcEmail { get; set; }
}

public class PortalAdminBillingGenerateInvoiceRequest
{
    public Guid TenantId { get; set; }
    public DateOnly BillingMonth { get; set; }
    public bool AllowDuplicateForMonth { get; set; }
    public bool PreviewOnly { get; set; }
}

public class PortalAdminBillingGenerateBulkInvoicesRequest
{
    public DateOnly BillingMonth { get; set; }
    public bool IncludeDisabledBusinesses { get; set; }
    public bool AllowDuplicateForMonth { get; set; }
    public bool PreviewOnly { get; set; }
    public IReadOnlyCollection<Guid> TenantIds { get; set; } = Array.Empty<Guid>();
}

public class PortalAdminBillingCustomInvoiceRequest
{
    public IReadOnlyCollection<Guid> TenantIds { get; set; } = Array.Empty<Guid>();
    public DateOnly BillingMonth { get; set; }
    public string? Currency { get; set; }
    public DateOnly? InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal? SoftwareFee { get; set; }
    public decimal? VesselFee { get; set; }
    public decimal? StaffFee { get; set; }
    public bool SaveAsBusinessCustomRate { get; set; }
    public bool AllowDuplicateForMonth { get; set; }
    public bool PreviewOnly { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyCollection<PortalAdminBillingCustomInvoiceLineItemRequest> LineItems { get; set; } = Array.Empty<PortalAdminBillingCustomInvoiceLineItemRequest>();
}

public class PortalAdminBillingCustomInvoiceLineItemRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal Rate { get; set; }
}

public class PortalAdminBillingGenerationResultDto
{
    public bool PreviewOnly { get; set; }
    public int GeneratedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyList<PortalAdminBillingGeneratedInvoiceDto> Invoices { get; set; } = Array.Empty<PortalAdminBillingGeneratedInvoiceDto>();
    public IReadOnlyList<PortalAdminBillingSkippedInvoiceDto> Skipped { get; set; } = Array.Empty<PortalAdminBillingSkippedInvoiceDto>();
}

public class PortalAdminBillingGeneratedInvoiceDto
{
    public Guid? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public DateOnly BillingMonth { get; set; }
    public int StaffCount { get; set; }
    public int VesselCount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "MVR";
}

public class PortalAdminBillingSkippedInvoiceDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class PortalAdminBillingCustomRateQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class PortalAdminBillingBusinessCustomRateDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public decimal SoftwareFee { get; set; }
    public decimal VesselFee { get; set; }
    public decimal StaffFee { get; set; }
    public string Currency { get; set; } = "MVR";
    public bool IsActive { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

public class PortalAdminBillingUpsertCustomRateRequest
{
    public Guid TenantId { get; set; }
    public decimal SoftwareFee { get; set; }
    public decimal VesselFee { get; set; }
    public decimal StaffFee { get; set; }
    public string Currency { get; set; } = "MVR";
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Notes { get; set; }
}

public class PortalAdminBillingBusinessOptionDto
{
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int StaffCount { get; set; }
    public int VesselCount { get; set; }
}
