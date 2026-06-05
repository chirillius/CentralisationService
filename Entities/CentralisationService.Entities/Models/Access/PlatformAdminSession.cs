namespace CentralisationService.Entities.Models.Access;

public sealed class PlatformAdminSession
{
    public Guid Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}
