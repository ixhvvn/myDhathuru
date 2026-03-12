using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MyDhathuru.Application.Common.Interfaces;
using MyDhathuru.Application.Common.Models;

namespace MyDhathuru.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public RequestContext GetContext()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new RequestContext();
        }

        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"), out var userId);
        Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId);

        return new RequestContext
        {
            UserId = userId == Guid.Empty ? null : userId,
            TenantId = tenantId == Guid.Empty ? null : tenantId,
            Email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email"),
            Role = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("role")
        };
    }
}
