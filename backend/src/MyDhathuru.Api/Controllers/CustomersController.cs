using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Customers.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/customers")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class CustomersController : BaseApiController
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> GetList([FromQuery] CustomerListQuery query, CancellationToken cancellationToken)
    {
        var result = await _customerService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdf([FromQuery] CustomerListQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _customerService.GeneratePdfAsync(query, cancellationToken);
        return File(bytes, "application/pdf", "customers.pdf");
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcel([FromQuery] CustomerListQuery query, CancellationToken cancellationToken)
    {
        var bytes = await _customerService.GenerateExcelAsync(query, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "customers.xlsx");
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _customerService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<CustomerDto>.Fail("Customer not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await _customerService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Customer created.");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await _customerService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Customer updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _customerService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Customer deleted.");
    }
}
