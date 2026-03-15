using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PaymentVouchers.Dtos;
using MyDhathuru.Domain.Enums;

namespace MyDhathuru.Api.Controllers;

[Route("api/payment-vouchers")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PaymentVouchersController : BaseApiController
{
    private readonly IPaymentVoucherService _paymentVoucherService;

    public PaymentVouchersController(IPaymentVoucherService paymentVoucherService)
    {
        _paymentVoucherService = paymentVoucherService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentVoucherListItemDto>>>> GetPaged([FromQuery] PaymentVoucherListQuery query, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<PaymentVoucherDetailDto>.Fail("Payment voucher not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> Create([FromBody] CreatePaymentVoucherRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Payment voucher created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> Update(Guid id, [FromBody] UpdatePaymentVoucherRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Payment voucher updated.");
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> Approve(Guid id, [FromBody] UpdatePaymentVoucherStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.UpdateStatusAsync(id, PaymentVoucherStatus.Approved, request.Notes, cancellationToken);
        return OkResponse(result, "Payment voucher approved.");
    }

    [HttpPost("{id:guid}/post")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> Post(Guid id, [FromBody] UpdatePaymentVoucherStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.UpdateStatusAsync(id, PaymentVoucherStatus.Posted, request.Notes, cancellationToken);
        return OkResponse(result, "Payment voucher posted.");
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDetailDto>>> Cancel(Guid id, [FromBody] UpdatePaymentVoucherStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentVoucherService.UpdateStatusAsync(id, PaymentVoucherStatus.Cancelled, request.Notes, cancellationToken);
        return OkResponse(result, "Payment voucher cancelled.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _paymentVoucherService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Payment voucher deleted.");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _paymentVoucherService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"payment-voucher-{id}.pdf");
    }
}
