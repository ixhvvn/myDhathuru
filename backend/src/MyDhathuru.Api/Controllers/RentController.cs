using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Rent.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/rent")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class RentController : BaseApiController
{
    private readonly IRentService _rentService;

    public RentController(IRentService rentService)
    {
        _rentService = rentService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RentEntryListItemDto>>>> GetPaged([FromQuery] RentEntryListQuery query, CancellationToken cancellationToken)
    {
        var result = await _rentService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RentEntryDetailDto>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _rentService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<RentEntryDetailDto>.Fail("Rent entry not found."));
        }

        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RentEntryDetailDto>>> Create([FromBody] CreateRentEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _rentService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Rent entry created.");
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RentEntryDetailDto>>> Update(Guid id, [FromBody] UpdateRentEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await _rentService.UpdateAsync(id, request, cancellationToken);
        return OkResponse(result, "Rent entry updated.");
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _rentService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Rent entry deleted.");
    }
}
