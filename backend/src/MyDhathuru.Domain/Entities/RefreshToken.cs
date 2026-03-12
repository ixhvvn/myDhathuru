using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
