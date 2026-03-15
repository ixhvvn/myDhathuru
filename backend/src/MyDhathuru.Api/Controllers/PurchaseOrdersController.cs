using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PurchaseOrders.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/purchase-orders")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PurchaseOrdersController : BaseApiController
{
    private readonly IPurchaseOrderService _purchaseOrderService;

    public PurchaseOrdersController(IPurchaseOrderService purchaseOrderService)
    {
        _purchaseOrderService = purchaseOrderService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseOrderListItemDto>>>> GetPaged([FromQuery] PurchaseOrderListQuery query, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<PurchaseOrderDetailDto>.Fail("Purchase order not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDetailDto>>> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Purchase order created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDetailDto>>> Update(Guid id, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _purchaseOrderService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Purchase order updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _purchaseOrderService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Purchase order deleted.");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _purchaseOrderService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"purchase-order-{id}.pdf");
    }
}
