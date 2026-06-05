namespace CentralisationService.Entities.Models.Access;

public sealed class CompanyInvitationView
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RoleKey { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? UsedAtUtc { get; init; }
    public Guid? UsedByAccountId { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public bool IsActive { get; init; }
}
