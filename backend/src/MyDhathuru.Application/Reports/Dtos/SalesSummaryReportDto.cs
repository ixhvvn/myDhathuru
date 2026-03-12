namespace MyDhathuru.Application.Reports.Dtos;

public class SalesSummaryReportDto
{
    public ReportMetaDto Meta { get; set; } = new();
    public int TotalInvoices { get; set; }
    public CurrencyTotalsDto TotalSales { get; set; } = new();
    public CurrencyTotalsDto TotalReceived { get; set; } = new();
    public CurrencyTotalsDto TotalPending { get; set; } = new();
    public int TotalCustomers { get; set; }
    public CurrencyTotalsDto TotalTax { get; set; } = new();
    public IReadOnlyCollection<SalesSummaryDailyRowDto> Rows { get; set; } = Array.Empty<SalesSummaryDailyRowDto>();
}
