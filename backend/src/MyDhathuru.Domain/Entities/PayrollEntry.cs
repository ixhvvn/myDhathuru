using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class PayrollEntry : TenantEntity
{
    public Guid PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;
    public Guid StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public decimal Basic { get; set; }
    public decimal ServiceAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PhoneAllowance { get; set; }
    public decimal GrossBase { get; set; }
    public decimal GrossAllowances { get; set; }
    public decimal SubTotal { get; set; }
    public int PeriodDays { get; set; }
    public int AttendedDays { get; set; }
    public int AbsentDays { get; set; }
    public decimal RatePerDay { get; set; }
    public decimal AbsentDeduction { get; set; }
    public int FoodAllowanceDays { get; set; }
    public decimal FoodAllowanceRate { get; set; }
    public decimal FoodAllowance { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal PensionDeduction { get; set; }
    public decimal SalaryAdvanceDeduction { get; set; }
    public decimal TotalPay { get; set; }
    public decimal NetPayable { get; set; }
    public SalarySlip? SalarySlip { get; set; }
}
