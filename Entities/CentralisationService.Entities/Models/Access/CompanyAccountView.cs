namespace CentralisationService.Entities.Models.Access;

public sealed class CompanyAccountView
{
    public Guid AccountId { get; init; }
    public Guid GrantId { get; init; }
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RoleKey { get; init; } = string.Empty;
    public string Status { get; init; } = "active";
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public DateTime? AccessExpiresAtUtc { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
    public string? LastLoginIp { get; init; }
}
