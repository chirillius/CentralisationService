namespace CentralisationService.Entities.Models.Access;

public sealed class CompanyAccessGrant
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid AccountId { get; init; }
    public string RoleKey { get; init; } = "company-operator";
    public string Status { get; init; } = "active";
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public DateTime? ExpiresAtUtc { get; init; }
    public bool IsEnabled { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; }
}
