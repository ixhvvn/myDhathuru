using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.Customers.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/vessels")]
[Authorize(Policy = "StaffOrAdmin")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class VesselsController : BaseApiController
{
    private readonly IVesselService _vesselService;

    public VesselsController(IVesselService vesselService)
    {
        _vesselService = vesselService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<VesselDto>>>> GetPaged([FromQuery] VesselListQuery query, CancellationToken cancellationToken)
    {
        var result = await _vesselService.GetPagedAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VesselDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _vesselService.GetAllAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<VesselDto>>> Create([FromBody] CreateVesselRequest request, CancellationToken cancellationToken)
    {
        var result = await _vesselService.CreateAsync(request, cancellationToken);
        return OkResponse(result, "Vessel created.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _vesselService.DeleteAsync(id, cancellationToken);
        return SuccessMessage("Vessel deleted.");
    }
}
