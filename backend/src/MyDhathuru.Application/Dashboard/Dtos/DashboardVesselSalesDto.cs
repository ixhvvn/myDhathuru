namespace MyDhathuru.Application.Dashboard.Dtos;

public class DashboardVesselSalesDto
{
    public Guid? VesselId { get; set; }
    public string VesselName { get; set; } = string.Empty;
    public decimal SalesMvr { get; set; }
    public decimal SalesUsd { get; set; }
    public decimal ContributionMvrPercentage { get; set; }
    public decimal ContributionUsdPercentage { get; set; }
}
