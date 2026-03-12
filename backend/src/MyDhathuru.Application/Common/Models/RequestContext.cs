namespace MyDhathuru.Application.Common.Models;

public sealed class RequestContext
{
    public Guid? UserId { get; init; }
    public Guid? TenantId { get; init; }
    public string? Email { get; init; }
    public string? Role { get; init; }
}
