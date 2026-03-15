using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Mira.Dtos;

public enum MiraReportType
{
    InputTaxStatement = 1,
    OutputTaxStatement = 2,
    BptIncomeStatement = 3,
    BptNotes = 4
}

public enum MiraPeriodMode
{
    Quarter = 1,
    Year = 2,
    CustomRange = 3
}

public class MiraReportQuery
{
    public MiraReportType ReportType { get; set; } = MiraReportType.InputTaxStatement;
    public MiraPeriodMode PeriodMode { get; set; } = MiraPeriodMode.Quarter;
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public int? Quarter { get; set; }
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
}

public class MiraReportExportRequest : MiraReportQuery
{
}

public class MiraReportPreviewDto
{
    public MiraReportType ReportType { get; set; }
    public MiraReportMetaDto Meta { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public MiraInputTaxStatementDto? InputTaxStatement { get; set; }
    public MiraOutputTaxStatementDto? OutputTaxStatement { get; set; }
    public BptIncomeStatementDto? BptIncomeStatement { get; set; }
    public BptNotesDto? BptNotes { get; set; }
}

public class MiraReportMetaDto
{
    public string Title { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateOnly RangeStart { get; set; }
    public DateOnly RangeEnd { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public string TaxableActivityNumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyTinNumber { get; set; } = string.Empty;
    public bool HasUsdTransactions { get; set; }
    public decimal UnconvertedUsdRevenue { get; set; }
    public decimal UnconvertedUsdExpenses { get; set; }
}

public class MiraInputTaxStatementDto
{
    public int TotalInvoices { get; set; }
    public decimal TotalInvoiceBase { get; set; }
    public decimal TotalGst6 { get; set; }
    public decimal TotalGst8 { get; set; }
    public decimal TotalGst12 { get; set; }
    public decimal TotalGst16 { get; set; }
    public decimal TotalGst17 { get; set; }
    public decimal TotalClaimableGst { get; set; }
    public List<MiraInputTaxRowDto> Rows { get; set; } = new();
}

public class MiraInputTaxRowDto
{
    public string SupplierTin { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal InvoiceTotalExcludingGst { get; set; }
    public decimal GstChargedAt6 { get; set; }
    public decimal GstChargedAt8 { get; set; }
    public decimal GstChargedAt12 { get; set; }
    public decimal GstChargedAt16 { get; set; }
    public decimal GstChargedAt17 { get; set; }
    public decimal TotalGst { get; set; }
    public string TaxableActivityNumber { get; set; } = string.Empty;
}

public class MiraOutputTaxStatementDto
{
    public int TotalInvoices { get; set; }
    public decimal TotalTaxableSupplies { get; set; }
    public decimal TotalZeroRatedSupplies { get; set; }
    public decimal TotalExemptSupplies { get; set; }
    public decimal TotalOutOfScopeSupplies { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public List<MiraOutputTaxRowDto> Rows { get; set; } = new();
}

public class MiraOutputTaxRowDto
{
    public string CustomerTin { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal TaxableSupplies { get; set; }
    public decimal ZeroRatedSupplies { get; set; }
    public decimal ExemptSupplies { get; set; }
    public decimal OutOfScopeSupplies { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
    public string TaxableActivityNumber { get; set; } = string.Empty;
}

public class BptIncomeStatementDto
{
    public decimal GrossSales { get; set; }
    public decimal SalesReturnsAndAllowances { get; set; }
    public decimal NetSales { get; set; }
    public decimal CostOfGoodsSold { get; set; }
    public decimal GrossProfit { get; set; }
    public List<BptIncomeLineDto> OperatingExpenses { get; set; } = new();
    public decimal TotalOperatingExpenses { get; set; }
    public decimal NetOperatingIncome { get; set; }
    public List<BptIncomeLineDto> OtherIncome { get; set; } = new();
    public decimal TotalOtherIncome { get; set; }
    public decimal NetIncome { get; set; }
}

public class BptIncomeLineDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class BptNotesDto
{
    public List<BptSalaryNoteRowDto> SalaryRows { get; set; } = new();
    public decimal TotalSalary { get; set; }
    public List<BptExpenseNoteSectionDto> Sections { get; set; } = new();
}

public class BptSalaryNoteRowDto
{
    public string StaffCode { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public decimal AverageBasicPerPeriod { get; set; }
    public decimal AverageAllowancePerPeriod { get; set; }
    public int PeriodCount { get; set; }
    public decimal TotalForPeriodRange { get; set; }
}

public class BptExpenseNoteSectionDto
{
    public string Title { get; set; } = string.Empty;
    public BptCategoryCode CategoryCode { get; set; }
    public decimal TotalAmount { get; set; }
    public List<BptExpenseNoteRowDto> Rows { get; set; } = new();
}

public class BptExpenseNoteRowDto
{
    public DateOnly Date { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string PayeeName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
