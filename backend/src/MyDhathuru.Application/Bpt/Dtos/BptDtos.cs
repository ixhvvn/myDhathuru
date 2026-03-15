using MyDhathuru.Application.Mira.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Bpt.Dtos;

public enum BptPeriodMode
{
    Quarter = 1,
    Year = 2,
    CustomRange = 3
}

public class BptReportQuery
{
    public BptPeriodMode PeriodMode { get; set; } = BptPeriodMode.Quarter;
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public int? Quarter { get; set; }
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
}

public class BptReportExportRequest : BptReportQuery
{
}

public class BptReportDto
{
    public BptReportMetaDto Meta { get; set; } = new();
    public List<string> ImportantNotes { get; set; } = new();
    public BptIncomeStatementDto Statement { get; set; } = new();
    public BptNotesDto Notes { get; set; } = new();
    public List<BptTraceTransactionDto> Transactions { get; set; } = new();
}

public class BptReportMetaDto
{
    public string Title { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly RangeStart { get; set; }
    public DateOnly RangeEnd { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string TaxableActivityNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyTinNumber { get; set; } = string.Empty;
}

public class BptTraceTransactionDto
{
    public Guid? SourceDocumentId { get; set; }
    public BptSourceModule SourceModule { get; set; }
    public string SourceDocumentNumber { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public int FinancialYear { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public string SourceStatus { get; set; } = string.Empty;
    public BptClassificationGroup ClassificationGroup { get; set; }
    public Guid? BptCategoryId { get; set; }
    public BptCategoryCode BptCategoryCode { get; set; }
    public string BptCategoryName { get; set; } = string.Empty;
    public bool IsAdjustment { get; set; }
    public string? Notes { get; set; }
}

public class BptCategoryLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BptCategoryCode Code { get; set; }
    public BptClassificationGroup ClassificationGroup { get; set; }
}

public class BptExpenseMappingDto
{
    public Guid? Id { get; set; }
    public Guid ExpenseCategoryId { get; set; }
    public string ExpenseCategoryName { get; set; } = string.Empty;
    public string ExpenseCategoryCode { get; set; } = string.Empty;
    public Guid BptCategoryId { get; set; }
    public BptCategoryCode BptCategoryCode { get; set; }
    public string BptCategoryName { get; set; } = string.Empty;
    public BptClassificationGroup ClassificationGroup { get; set; }
    public BptSourceModule? SourceModule { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class UpsertBptExpenseMappingRequest
{
    public Guid BptCategoryId { get; set; }
    public BptSourceModule? SourceModule { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public class BptExchangeRateListQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Currency { get; set; }
}

public class BptExchangeRateDto
{
    public Guid Id { get; set; }
    public DateOnly RateDate { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal RateToMvr { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertBptExchangeRateRequest
{
    public DateOnly RateDate { get; set; }
    public string? Currency { get; set; }
    public decimal RateToMvr { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SalesAdjustmentListQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public SalesAdjustmentType? AdjustmentType { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
}

public class SalesAdjustmentDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public SalesAdjustmentType AdjustmentType { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public string? RelatedInvoiceNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string? Notes { get; set; }
}

public class CreateSalesAdjustmentRequest
{
    public SalesAdjustmentType AdjustmentType { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid? RelatedInvoiceId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Currency { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal? ExchangeRate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}

public class UpdateSalesAdjustmentRequest : CreateSalesAdjustmentRequest
{
}

public class OtherIncomeEntryListQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
}

public class OtherIncomeEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CounterpartyName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string? Notes { get; set; }
}

public class CreateOtherIncomeEntryRequest
{
    public DateOnly TransactionDate { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CounterpartyName { get; set; }
    public required string Description { get; set; }
    public string? Currency { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal? ExchangeRate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}

public class UpdateOtherIncomeEntryRequest : CreateOtherIncomeEntryRequest
{
}

public class BptAdjustmentListQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public Guid? BptCategoryId { get; set; }
    public ApprovalStatus? ApprovalStatus { get; set; }
}

public class BptAdjustmentDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid BptCategoryId { get; set; }
    public string BptCategoryName { get; set; } = string.Empty;
    public BptCategoryCode BptCategoryCode { get; set; }
    public BptClassificationGroup ClassificationGroup { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal ExchangeRate { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal AmountMvr { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public string? Notes { get; set; }
}

public class CreateBptAdjustmentRequest
{
    public DateOnly TransactionDate { get; set; }
    public required string Description { get; set; }
    public Guid BptCategoryId { get; set; }
    public string? Currency { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal? ExchangeRate { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Approved;
    public string? Notes { get; set; }
}

public class UpdateBptAdjustmentRequest : CreateBptAdjustmentRequest
{
}
