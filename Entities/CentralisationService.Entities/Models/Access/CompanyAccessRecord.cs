namespace CentralisationService.Entities.Models.Access;

public sealed class CompanyAccessRecord
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public CompanyStatus Status { get; init; } = CompanyStatus.Active;
    public DateTime? AccessExpiresAtUtc { get; init; }
    public DateTime? DisabledAtUtc { get; init; }
    public string? DisabledReason { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
