using System.Security.Claims;
using MyDhathuru.Application.Common.Interfaces;

namespace MyDhathuru.Api.Middlewares;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService currentTenantService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                currentTenantService.SetTenant(tenantId);
            }
        }

        await _next(context);
    }
}
