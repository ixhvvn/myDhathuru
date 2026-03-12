using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=mydhathuru;Username=postgres;Password=postgres");
        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantService(), new DesignTimeUserService());
    }

    private sealed class DesignTimeTenantService : ICurrentTenantService
    {
        public Guid? TenantId => null;
        public void SetTenant(Guid? tenantId)
        {
        }
    }

    private sealed class DesignTimeUserService : ICurrentUserService
    {
        public RequestContext GetContext() => new();
    }
}
