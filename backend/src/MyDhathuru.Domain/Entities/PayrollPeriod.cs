using MyDhathuru.Domain.Common;
using MyDhathuru.Domain.Enums;
namespace MyDhathuru.Domain.Entities;
public class PayrollPeriod : TenantEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int PeriodDays { get; set; }
    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;
    public decimal TotalNetPayable { get; set; }
    public ICollection<PayrollEntry> Entries { get; set; } = new List<PayrollEntry>();
}
