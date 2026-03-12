using MyDhathuru.Application.Common.Interfaces;

namespace MyDhathuru.Infrastructure.Services;

public class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid? tenantId)
    {
        TenantId = tenantId;
    }
}
