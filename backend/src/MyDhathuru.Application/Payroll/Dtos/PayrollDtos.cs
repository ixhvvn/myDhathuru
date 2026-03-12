using MyDhathuru.Application.Common.Models;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Application.Payroll.Dtos;

public class StaffListQuery : PaginationQuery
{
}

public class StaffDto
{
    public Guid Id { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public decimal Basic { get; set; }
    public decimal ServiceAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PhoneAllowance { get; set; }
    public decimal FoodRate { get; set; }
}

public class CreateStaffRequest
{
    public required string StaffId { get; set; }
    public required string StaffName { get; set; }
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public decimal Basic { get; set; }
    public decimal ServiceAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PhoneAllowance { get; set; }
    public decimal FoodRate { get; set; }
}

public class UpdateStaffRequest : CreateStaffRequest
{
}

public class CreatePayrollPeriodRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class PayrollPeriodDto
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int PeriodDays { get; set; }
    public PayrollPeriodStatus Status { get; set; }
    public decimal TotalNetPayable { get; set; }
}

public class PayrollEntryDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }

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
}

public class PayrollPeriodDetailDto : PayrollPeriodDto
{
    public List<PayrollEntryDto> Entries { get; set; } = new();
}

public class UpdatePayrollEntryRequest
{
    public int? AttendedDays { get; set; }
    public int? FoodAllowanceDays { get; set; }
    public decimal? FoodAllowanceRate { get; set; }
    public decimal? OvertimePay { get; set; }
    public decimal? SalaryAdvanceDeduction { get; set; }
}

public class SalarySlipDto
{
    public Guid PayrollEntryId { get; set; }
    public string SlipNo { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? WorkSite { get; set; }
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }

    public decimal BasicSalary { get; set; }
    public decimal ServiceAllowance { get; set; }
    public decimal OtherAllowance { get; set; }
    public decimal PhoneAllowance { get; set; }
    public decimal FoodAllowance { get; set; }
    public decimal OvertimePay { get; set; }
    public int FoodAllowanceDays { get; set; }
    public decimal FoodAllowanceRate { get; set; }

    public int AttendedDays { get; set; }
    public int AbsentDays { get; set; }
    public decimal RatePerDay { get; set; }
    public decimal AbsentDeduction { get; set; }
    public decimal PensionDeduction { get; set; }
    public decimal SalaryAdvanceDeduction { get; set; }

    public decimal TotalSalary { get; set; }
    public decimal TotalDeduction { get; set; }
    public decimal TotalPayable { get; set; }
}
