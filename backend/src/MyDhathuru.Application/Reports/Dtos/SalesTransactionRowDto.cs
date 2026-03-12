namespace MyDhathuru.Application.Reports.Dtos;

public class SalesTransactionRowDto
{
    public string InvoiceNo { get; set; } = string.Empty;
    public DateOnly DateIssued { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Vessel { get; set; } = "Unassigned Vessel";
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "-";
    public DateOnly? ReceivedOn { get; set; }
    public decimal Balance { get; set; }
}
