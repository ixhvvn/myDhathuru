using MyDhathuru.Domain.Common;
namespace MyDhathuru.Domain.Entities;
public class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public bool IsUsed => UsedAt is not null;
}
