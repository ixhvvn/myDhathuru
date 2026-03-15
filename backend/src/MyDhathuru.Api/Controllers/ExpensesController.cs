using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/expenses")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class ExpensesController : BaseApiController
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet("ledger")]
    public async Task<ActionResult<ApiResponse<PagedResult<ExpenseLedgerRowDto>>>> GetLedger([FromQuery] ExpenseLedgerQuery query, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetLedgerAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<ExpenseSummaryDto>>> GetSummary([FromQuery] ExpenseSummaryQuery query, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetSummaryAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] ExpenseLedgerQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _expenseService.GeneratePdfAsync(query, cancellationToken);
        return File(bytes, "application/pdf", "expense-ledger.pdf");
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] ExpenseLedgerQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _expenseService.GenerateExcelAsync(query, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "expense-ledger.xlsx");
    }

    [HttpGet("manual/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseEntryDetailDto>>> GetManualById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetManualEntryByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ExpenseEntryDetailDto>.Fail("Expense entry not found."));
        }

        return OkResponse(result);
    }

    [HttpPost("manual")]
    public async Task<ActionResult<ApiResponse<ExpenseEntryDetailDto>>> CreateManual([FromBody] CreateManualExpenseEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.CreateManualEntryAsync(request, cancellationToken);
        return OkResponse(result, "Expense entry created.");
    }

    [HttpPut("manual/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseEntryDetailDto>>> UpdateManual(Guid id, [FromBody] UpdateManualExpenseEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseService.UpdateManualEntryAsync(id, request, cancellationToken);
        return OkResponse(result, "Expense entry updated.");
    }

    [HttpDelete("manual/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteManual(Guid id, CancellationToken cancellationToken)
    {
        await _expenseService.DeleteManualEntryAsync(id, cancellationToken);
        return SuccessMessage("Expense entry deleted.");
    }
}
