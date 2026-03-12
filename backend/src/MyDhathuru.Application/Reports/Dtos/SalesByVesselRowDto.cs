namespace MyDhathuru.Application.Reports.Dtos;

public class SalesByVesselRowDto
{
    public string Vessel { get; set; } = "Unassigned Vessel";
    public string Currency { get; set; } = "MVR";
    public int TransactionCount { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal PercentageOfCurrencySales { get; set; }
}
