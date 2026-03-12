using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Payroll.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPayrollService
{
    Task<PagedResult<StaffDto>> GetStaffAsync(StaffListQuery query, CancellationToken cancellationToken = default);
    Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);
    Task<StaffDto> UpdateStaffAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken = default);
    Task DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollPeriodDto>> GetPeriodsAsync(CancellationToken cancellationToken = default);
    Task<PayrollPeriodDetailDto> CreatePeriodAsync(CreatePayrollPeriodRequest request, CancellationToken cancellationToken = default);
    Task<PayrollPeriodDetailDto> GetPeriodAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken = default);
    Task RecalculatePeriodAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PayrollEntryDto> UpdatePayrollEntryAsync(Guid periodId, Guid entryId, UpdatePayrollEntryRequest request, CancellationToken cancellationToken = default);
    Task<SalarySlipDto> GenerateSalarySlipAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);
    Task<SalarySlipDto?> GetSalarySlipAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateSalarySlipPdfAsync(Guid payrollEntryId, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePayrollPeriodPdfAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default);
    Task<byte[]> GeneratePayrollPeriodExcelAsync(Guid payrollPeriodId, CancellationToken cancellationToken = default);
}
