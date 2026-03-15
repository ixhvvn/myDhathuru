using MyDhathuru.Domain.Common;

namespace MyDhathuru.Domain.Entities;

public class ExchangeRate : TenantEntity
{
    public DateOnly RateDate { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal RateToMvr { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
