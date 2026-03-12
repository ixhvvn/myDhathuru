namespace MyDhathuru.Application.Dashboard.Dtos;

public class DashboardTopCustomerDto
{
    public int Rank { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal SalesMvr { get; set; }
    public decimal SalesUsd { get; set; }
    public int InvoiceCount { get; set; }
    public decimal ContributionMvrPercentage { get; set; }
    public decimal ContributionUsdPercentage { get; set; }
    public string Initials { get; set; } = string.Empty;
}
