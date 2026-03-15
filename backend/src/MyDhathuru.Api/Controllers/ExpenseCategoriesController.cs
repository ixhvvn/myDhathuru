using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Expenses.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/expense-categories")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class ExpenseCategoriesController : BaseApiController
{
    private readonly IExpenseCategoryService _expenseCategoryService;

    public ExpenseCategoriesController(IExpenseCategoryService expenseCategoryService)
    {
        _expenseCategoryService = expenseCategoryService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ExpenseCategoryDto>>>> GetPaged([FromQuery] ExpenseCategoryListQuery query, CancellationToken cancellationToken)
    {
        var result = await _expenseCategoryService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExpenseCategoryLookupDto>>>> GetLookup(CancellationToken cancellationToken)
    {
        var result = await _expenseCategoryService.GetLookupAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseCategoryDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _expenseCategoryService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<ExpenseCategoryDto>.Fail("Expense category not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ExpenseCategoryDto>>> Create([FromBody] CreateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseCategoryService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Expense category created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExpenseCategoryDto>>> Update(Guid id, [FromBody] UpdateExpenseCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _expenseCategoryService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Expense category updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _expenseCategoryService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Expense category deleted.");
    }
}
