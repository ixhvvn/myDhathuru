using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.DeliveryNotes.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/delivery-notes")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class DeliveryNotesController : BaseApiController
{
    private readonly IDeliveryNoteService _deliveryNoteService;

    public DeliveryNotesController(IDeliveryNoteService deliveryNoteService)
    {
        _deliveryNoteService = deliveryNoteService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DeliveryNoteListItemDto>>>> GetPaged([FromQuery] DeliveryNoteListQuery query, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<DeliveryNoteDetailDto>.Fail("Delivery note not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> Create([FromBody] CreateDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Delivery note created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDetailDto>>> Update(Guid id, [FromBody] UpdateDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Delivery note updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _deliveryNoteService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Delivery note deleted.");
    }

    [HttpPost("clear-all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAll([FromBody] ConfirmPasswordRequest request, CancellationToken cancellationToken)
    {
        await _deliveryNoteService.ClearAllAsync(request.Password, cancellationToken);
        return SuccessMessage("All delivery notes deleted.");
    }

    [HttpPost("{id:guid}/create-invoice")]
    public async Task<ActionResult<ApiResponse<CreateInvoiceFromDeliveryNoteResultDto>>> CreateInvoice(Guid id, [FromBody] CreateInvoiceFromDeliveryNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _deliveryNoteService.CreateInvoiceFromDeliveryNoteAsync(id, request, cancellationToken);
        return OkResponse(result, "Invoice created from delivery note.");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _deliveryNoteService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"delivery-note-{id}.pdf");
    }
}
