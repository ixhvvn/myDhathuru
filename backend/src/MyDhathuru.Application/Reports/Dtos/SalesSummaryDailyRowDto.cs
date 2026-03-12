namespace MyDhathuru.Application.Reports.Dtos;

public class SalesSummaryDailyRowDto
{
    public DateOnly Date { get; set; }
    public int InvoiceCount { get; set; }
    public decimal SalesMvr { get; set; }
    public decimal SalesUsd { get; set; }
    public decimal ReceivedMvr { get; set; }
    public decimal ReceivedUsd { get; set; }
    public decimal PendingMvr { get; set; }
    public decimal PendingUsd { get; set; }
}
