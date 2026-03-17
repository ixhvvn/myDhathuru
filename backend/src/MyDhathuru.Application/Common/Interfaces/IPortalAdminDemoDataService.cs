using MyDhathuru.Application.PortalAdmin.Dtos;

namespace MyDhathuru.Application.Common.Interfaces;

public interface IPortalAdminDemoDataService
{
    Task<PortalAdminDemoDataSeedResultDto> SeedBusinessDemoDataAsync(Guid tenantId, Guid performedByUserId, CancellationToken cancellationToken = default);
}
