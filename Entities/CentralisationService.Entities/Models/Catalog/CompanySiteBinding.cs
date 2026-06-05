namespace CentralisationService.Entities.Models.Catalog;

public sealed class CompanySiteBinding
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyKey { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public string ServerBaseUrl { get; init; } = string.Empty;
    public string ConnectorAccessToken { get; init; } = string.Empty;
    public int CleaningDay { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? DisabledAtUtc { get; init; }
}
