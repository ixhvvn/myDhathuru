using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Payroll.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/payroll")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PayrollController : BaseApiController
{
    private readonly IPayrollService _payrollService;

    public PayrollController(IPayrollService payrollService)
    {
        _payrollService = payrollService;
    }

    [HttpGet("staff")]
    public async Task<ActionResult<ApiResponse<PagedResult<StaffDto>>>> GetStaff([FromQuery] StaffListQuery query, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetStaffAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("staff")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<StaffDto>>> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var result = await _payrollService.CreateStaffAsync(request, cancellationToken);
        return OkResponse(result, "Staff created.");
    }

    [HttpPut("staff/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<StaffDto>>> UpdateStaff(Guid id, [FromBody] UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        var result = await _payrollService.UpdateStaffAsync(id, request, cancellationToken);
        return OkResponse(result, "Staff updated.");
    }

    [HttpDelete("staff/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteStaff(Guid id, CancellationToken cancellationToken)
    {
        await _payrollService.DeleteStaffAsync(id, cancellationToken);
        return SuccessMessage("Staff deleted.");
    }

    [HttpGet("periods")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PayrollPeriodDto>>>> GetPeriods(CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetPeriodsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("periods")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDetailDto>>> CreatePeriod([FromBody] CreatePayrollPeriodRequest request, CancellationToken cancellationToken)
    {
        var result = await _payrollService.CreatePeriodAsync(request, cancellationToken);
        return OkResponse(result, "Payroll period created.");
    }

    [HttpGet("periods/{id:guid}")]
    public async Task<ActionResult<ApiResponse<PayrollPeriodDetailDto>>> GetPeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetPeriodAsync(id, cancellationToken);
        return OkResponse(result);
    }

    [HttpDelete("periods/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePeriod(Guid id, CancellationToken cancellationToken)
    {
        await _payrollService.DeletePeriodAsync(id, cancellationToken);
        return SuccessMessage("Payroll period deleted.");
    }

    [HttpPost("periods/{id:guid}/recalculate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> RecalculatePeriod(Guid id, CancellationToken cancellationToken)
    {
        await _payrollService.RecalculatePeriodAsync(id, cancellationToken);
        return SuccessMessage("Payroll period recalculated.");
    }

    [HttpPut("periods/{periodId:guid}/entries/{entryId:guid}")]
    public async Task<ActionResult<ApiResponse<PayrollEntryDto>>> UpdateEntry(Guid periodId, Guid entryId, [FromBody] UpdatePayrollEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _payrollService.UpdatePayrollEntryAsync(periodId, entryId, request, cancellationToken);
        return OkResponse(result, "Payroll entry updated.");
    }

    [HttpPost("entries/{payrollEntryId:guid}/salary-slip")]
    public async Task<ActionResult<ApiResponse<SalarySlipDto>>> GenerateSalarySlip(Guid payrollEntryId, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GenerateSalarySlipAsync(payrollEntryId, cancellationToken);
        return OkResponse(result, "Salary slip generated.");
    }

    [HttpGet("entries/{payrollEntryId:guid}/salary-slip")]
    public async Task<ActionResult<ApiResponse<SalarySlipDto>>> GetSalarySlip(Guid payrollEntryId, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetSalarySlipAsync(payrollEntryId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<SalarySlipDto>.Fail("Salary slip not found."));
        }

        return OkResponse(result);
    }

    [HttpGet("entries/{payrollEntryId:guid}/salary-slip/export")]
    public async Task<IActionResult> ExportSalarySlip(Guid payrollEntryId, CancellationToken cancellationToken)
    {
        var bytes = await _payrollService.GenerateSalarySlipPdfAsync(payrollEntryId, cancellationToken);
        return File(bytes, "application/pdf", $"salary-slip-{payrollEntryId}.pdf");
    }

    [HttpGet("periods/{payrollPeriodId:guid}/export/pdf")]
    public async Task<IActionResult> ExportPayrollPeriodPdf(Guid payrollPeriodId, CancellationToken cancellationToken)
    {
        var bytes = await _payrollService.GeneratePayrollPeriodPdfAsync(payrollPeriodId, cancellationToken);
        return File(bytes, "application/pdf", $"payroll-detail-{payrollPeriodId}.pdf");
    }

    [HttpGet("periods/{payrollPeriodId:guid}/export/excel")]
    public async Task<IActionResult> ExportPayrollPeriodExcel(Guid payrollPeriodId, CancellationToken cancellationToken)
    {
        var bytes = await _payrollService.GeneratePayrollPeriodExcelAsync(payrollPeriodId, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"payroll-detail-{payrollPeriodId}.xlsx");
    }
}
