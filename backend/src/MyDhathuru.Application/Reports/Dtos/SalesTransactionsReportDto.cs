namespace MyDhathuru.Application.Reports.Dtos;

public class SalesTransactionsReportDto
{
    public ReportMetaDto Meta { get; set; } = new();
    public int TotalTransactions { get; set; }
    public CurrencyTotalsDto TotalSales { get; set; } = new();
    public CurrencyTotalsDto TotalReceived { get; set; } = new();
    public CurrencyTotalsDto TotalPending { get; set; } = new();
    public IReadOnlyCollection<SalesTransactionRowDto> Rows { get; set; } = Array.Empty<SalesTransactionRowDto>();
}
