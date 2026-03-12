namespace MyDhathuru.Application.Dashboard.Dtos;

public class DashboardSummaryDto
{
    public int CurrentMonthInvoices { get; set; }
    public int LastMonthInvoices { get; set; }
    public DashboardTrendDto InvoicesTrend { get; set; } = new();

    public DashboardCurrencyAmountDto CurrentMonthSales { get; set; } = new();
    public DashboardCurrencyAmountDto LastMonthSales { get; set; } = new();
    public DashboardCurrencyTrendDto SalesTrend { get; set; } = new();

    public DashboardCurrencyAmountDto CurrentMonthPending { get; set; } = new();
    public DashboardCurrencyAmountDto LastMonthPending { get; set; } = new();
    public DashboardCurrencyTrendDto PendingTrend { get; set; } = new();

    public int CurrentMonthDeliveryNotes { get; set; }
    public int LastMonthDeliveryNotes { get; set; }
    public DashboardTrendDto DeliveryNotesTrend { get; set; } = new();

    public int CurrentMonthNewCustomers { get; set; }
    public int LastMonthNewCustomers { get; set; }
    public DashboardTrendDto NewCustomersTrend { get; set; } = new();

    public decimal CurrentMonthPayroll { get; set; }
    public decimal LastMonthPayroll { get; set; }
    public DashboardTrendDto PayrollTrend { get; set; } = new();
}

public class DashboardCurrencyAmountDto
{
    public decimal Mvr { get; set; }
    public decimal Usd { get; set; }
}

public class DashboardTrendDto
{
    public decimal? Percentage { get; set; }
    public string Direction { get; set; } = "neutral";
    public string Label { get; set; } = "No activity";
}

public class DashboardCurrencyTrendDto
{
    public DashboardTrendDto Mvr { get; set; } = new();
    public DashboardTrendDto Usd { get; set; } = new();
}
