using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Quotations.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/quotes")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class QuotesController : BaseApiController
{
    private readonly IQuotationService _quotationService;

    public QuotesController(IQuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QuotationListItemDto>>>> GetPaged([FromQuery] QuotationListQuery query, CancellationToken cancellationToken)
    {
        var result = await _quotationService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuotationDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _quotationService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<QuotationDetailDto>.Fail("Quotation not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<QuotationDetailDto>>> Create([FromBody] CreateQuotationRequest request, CancellationToken cancellationToken)
    {
        var result = await _quotationService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Quotation created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<QuotationDetailDto>>> Update(Guid id, [FromBody] UpdateQuotationRequest request, CancellationToken cancellationToken)
    {
        var result = await _quotationService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Quotation updated.");
    }

    [HttpPost("{id:guid}/convert-to-sale")]
    public async Task<ActionResult<ApiResponse<QuotationConversionResultDto>>> ConvertToSale(Guid id, CancellationToken cancellationToken)
    {
        var result = await _quotationService.ConvertToSaleAsync(id, cancellationToken);
        var message = result.AlreadyConverted
            ? "Quotation already converted to sales invoice."
            : "Quotation converted to sales invoice.";
        return OkResponse(result, message);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _quotationService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Quotation deleted.");
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var bytes = await _quotationService.GeneratePdfAsync(id, cancellationToken);
        return File(bytes, "application/pdf", $"quotation-{id}.pdf");
    }
}
