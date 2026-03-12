namespace MyDhathuru.Application.Dashboard.Dtos;

public class DashboardAnalyticsDto
{
    public DashboardSummaryDto Summary { get; set; } = new();
    public IReadOnlyCollection<DashboardTopCustomerDto> TopCustomers { get; set; } = Array.Empty<DashboardTopCustomerDto>();
    public IReadOnlyCollection<DashboardMonthlySalesDto> SalesLast6Months { get; set; } = Array.Empty<DashboardMonthlySalesDto>();
    public IReadOnlyCollection<DashboardVesselSalesDto> VesselSales { get; set; } = Array.Empty<DashboardVesselSalesDto>();
}
