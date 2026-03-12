namespace MyDhathuru.Application.Dashboard.Dtos;

public class DashboardMonthlySalesDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal SalesMvr { get; set; }
    public decimal SalesUsd { get; set; }
}
