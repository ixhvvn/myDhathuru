using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Dtos;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Invoices.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/invoices")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class InvoicesController : BaseApiController
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<InvoiceListItemDto>>>> GetPaged([FromQuery] InvoiceListQuery query, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<InvoiceDetailDto>.Fail("Invoice not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Invoice created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<InvoiceDetailDto>>> Update(Guid id, [FromBody] UpdateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Invoice updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _invoiceService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Invoice deleted.");
    }

    [HttpPost("{id:guid}/receive-payment")]
    public async Task<ActionResult<ApiResponse<InvoicePaymentDto>>> ReceivePayment(Guid id, [FromBody] ReceiveInvoicePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.ReceivePaymentAsync(id, request, cancellationToken);
        return OkResponse(result, "Payment recorded.");
    }

    [HttpPost("clear-all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAll([FromBody] ConfirmPasswordRequest request, CancellationToken cancellationToken)
    {
        await _invoiceService.ClearAllAsync(request.Password, cancellationToken);
        return SuccessMessage("All invoices deleted.");
    }

    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InvoicePaymentDto>>>> GetPayments(Guid id, CancellationToken cancellationToken)
    {
        var result = await _invoiceService.GetPaymentHistoryAsync(id, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("{id:guid}/email")]
    public async Task<ActionResult<ApiResponse<object>>> SendEmail(Guid id, [FromBody] SendInvoiceEmailRequest request, CancellationToken cancellationToken)
    {
        await _invoiceService.SendEmailAsync(id, request, cancellationToken);
        return SuccessMessage("Invoice emailed.");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _invoiceService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"invoice-{id}.pdf");
    }
}
