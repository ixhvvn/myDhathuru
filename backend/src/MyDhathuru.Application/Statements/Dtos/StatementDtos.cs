namespace MyDhathuru.Application.Statements.Dtos;

public class StatementCurrencyTotalsDto
{
    public decimal Mvr { get; set; }
    public decimal Usd { get; set; }
}

public class AccountStatementRowDto
{
    public int Index { get; set; }
    public DateOnly? Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Currency { get; set; } = "MVR";
    public decimal Amount { get; set; }
    public decimal Payments { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public decimal Balance { get; set; }
}

public class AccountStatementDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Year { get; set; }
    public string StatementNo { get; set; } = string.Empty;
    public StatementCurrencyTotalsDto OpeningBalance { get; set; } = new();
    public StatementCurrencyTotalsDto TotalInvoiced { get; set; } = new();
    public StatementCurrencyTotalsDto TotalReceived { get; set; } = new();
    public StatementCurrencyTotalsDto TotalPending { get; set; } = new();
    public List<AccountStatementRowDto> Rows { get; set; } = new();
}

public class SaveOpeningBalanceRequest
{
    public Guid CustomerId { get; set; }
    public int Year { get; set; }
    public decimal OpeningBalanceMvr { get; set; }
    public decimal OpeningBalanceUsd { get; set; }
    public string? Notes { get; set; }
}
