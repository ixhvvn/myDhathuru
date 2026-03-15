using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Suppliers.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/suppliers")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class SuppliersController : BaseApiController
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> GetPaged([FromQuery] SupplierListQuery query, CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SupplierLookupDto>>>> GetLookup(CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetLookupAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<SupplierDto>.Fail("Supplier not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Supplier created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Update(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Supplier updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _supplierService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Supplier deleted.");
    }
}
