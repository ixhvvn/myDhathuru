using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPortalAdminEmailService
{
    Task<IReadOnlyList<PortalAdminEmailBusinessOptionDto>> GetBusinessOptionsAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminEmailCampaignListItemDto>> GetCampaignsAsync(PortalAdminEmailCampaignQuery query, CancellationToken cancellationToken = default);
    Task<PortalAdminEmailCampaignSendResultDto> SendCampaignAsync(PortalAdminSendEmailCampaignRequest request, CancellationToken cancellationToken = default);
}
