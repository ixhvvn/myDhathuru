using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/signup-requests")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminSignupRequestsController : BaseApiController
{
    private readonly IPortalAdminService _portalAdminService;

    public PortalAdminSignupRequestsController(IPortalAdminService portalAdminService)
    {
        _portalAdminService = portalAdminService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SignupRequestListItemDto>>>> GetList([FromQuery] SignupRequestListQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetSignupRequestsAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("counts")]
    public async Task<ActionResult<ApiResponse<SignupRequestCountsDto>>> GetCounts(CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetSignupRequestCountsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("{requestId:guid}")]
    public async Task<ActionResult<ApiResponse<SignupRequestDetailDto>>> GetById(Guid requestId, CancellationToken cancellationToken)
    {
        var result = await _portalAdminService.GetSignupRequestByIdAsync(requestId, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("{requestId:guid}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> Approve(Guid requestId, [FromBody] ApproveSignupRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminService.ApproveSignupRequestAsync(requestId, request, cancellationToken);
        return SuccessMessage("Signup request approved.");
    }

    [HttpPost("{requestId:guid}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> Reject(Guid requestId, [FromBody] RejectSignupRequest request, CancellationToken cancellationToken)
    {
        await _portalAdminService.RejectSignupRequestAsync(requestId, request, cancellationToken);
        return SuccessMessage("Signup request rejected.");
    }
}

