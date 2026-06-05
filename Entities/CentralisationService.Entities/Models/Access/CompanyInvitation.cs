namespace CentralisationService.Entities.Models.Access;

public sealed class CompanyInvitation
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public string RoleKey { get; init; } = "company-operator";
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? UsedAtUtc { get; init; }
    public Guid? UsedByAccountId { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
