using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class CustomerOpeningBalance : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int Year { get; set; }
    public decimal OpeningBalanceMvr { get; set; }
    public decimal OpeningBalanceUsd { get; set; }
    public string? Notes { get; set; }
}
