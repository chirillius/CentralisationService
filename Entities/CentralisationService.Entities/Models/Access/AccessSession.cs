namespace CentralisationService.Entities.Models.Access;

public sealed class AccessSession
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid AccountId { get; init; }
    public Guid GrantId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}
