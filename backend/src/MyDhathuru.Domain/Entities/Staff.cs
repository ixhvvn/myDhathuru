using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class Staff : TenantEntity
{
    public required string StaffId { get; set; }
    public required string StaffName { get; set; }
    public string? IdNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly? HiredDate { get; set; }
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
    public ICollection<PayrollEntry> PayrollEntries { get; set; } = new List<PayrollEntry>();
    public ICollection<StaffConductForm> ConductForms { get; set; } = new List<StaffConductForm>();
}
