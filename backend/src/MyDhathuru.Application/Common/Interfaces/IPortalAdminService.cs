using MyDhathuru.Application.Common.Models;
using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPortalAdminService
{
    Task<PortalAdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SignupRequestListItemDto>> GetSignupRequestsAsync(SignupRequestListQuery query, CancellationToken cancellationToken = default);
    Task<SignupRequestCountsDto> GetSignupRequestCountsAsync(CancellationToken cancellationToken = default);
    Task<SignupRequestDetailDto> GetSignupRequestByIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task ApproveSignupRequestAsync(Guid requestId, ApproveSignupRequest request, CancellationToken cancellationToken = default);
    Task RejectSignupRequestAsync(Guid requestId, RejectSignupRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminBusinessListItemDto>> GetBusinessesAsync(PortalAdminBusinessListQuery query, CancellationToken cancellationToken = default);
    Task<PortalAdminBusinessDetailDto> GetBusinessByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task DisableBusinessAsync(Guid tenantId, PortalAdminSetBusinessStatusRequest request, CancellationToken cancellationToken = default);
    Task EnableBusinessAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task DeleteBusinessPermanentlyAsync(Guid tenantId, PortalAdminDeleteBusinessRequest request, CancellationToken cancellationToken = default);
    Task UpdateBusinessLoginDetailsAsync(Guid tenantId, PortalAdminUpdateBusinessLoginRequest request, CancellationToken cancellationToken = default);
    Task SendBusinessPasswordResetLinkAsync(Guid tenantId, PortalAdminSendResetLinkRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminBusinessUserDto>> GetBusinessUsersAsync(PortalAdminBusinessUsersQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalAdminAuditLogDto>> GetAuditLogsAsync(PortalAdminAuditLogQuery query, CancellationToken cancellationToken = default);
}

