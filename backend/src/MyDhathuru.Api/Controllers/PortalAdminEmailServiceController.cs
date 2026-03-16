using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDhathuru.Api.Common;
using MyDhathuru.Api.Filters;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Api.Controllers;

[Route("api/portal-admin/email-service")]
[Authorize(Policy = "SuperAdminOnly")]
[ServiceFilter(typeof(ValidationActionFilter))]
public class PortalAdminEmailServiceController : BaseApiController
{
    private readonly IPortalAdminEmailService _portalAdminEmailService;

    public PortalAdminEmailServiceController(IPortalAdminEmailService portalAdminEmailService)
    {
        _portalAdminEmailService = portalAdminEmailService;
    }

    [HttpGet("business-options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PortalAdminEmailBusinessOptionDto>>>> GetBusinessOptions(CancellationToken cancellationToken)
    {
        var result = await _portalAdminEmailService.GetBusinessOptionsAsync(cancellationToken);
        return OkResponse(result);
    }

    [HttpGet("campaigns")]
    public async Task<ActionResult<ApiResponse<PagedResult<PortalAdminEmailCampaignListItemDto>>>> GetCampaigns([FromQuery] PortalAdminEmailCampaignQuery query, CancellationToken cancellationToken)
    {
        var result = await _portalAdminEmailService.GetCampaignsAsync(query, cancellationToken);
        return OkResponse(result);
    }

    [HttpPost("campaigns/send")]
    public async Task<ActionResult<ApiResponse<PortalAdminEmailCampaignSendResultDto>>> SendCampaign([FromBody] PortalAdminSendEmailCampaignRequest request, CancellationToken cancellationToken)
    {
        var result = await _portalAdminEmailService.SendCampaignAsync(request, cancellationToken);
        return OkResponse(result, "Email campaign processed.");
    }
}
