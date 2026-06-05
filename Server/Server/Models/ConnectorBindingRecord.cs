namespace Server.Models;

public sealed class ConnectorBindingRecord
{
    public Guid CompanyId { get; init; }
    public string CompanyKey { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public string CentralServerUrl { get; init; } = string.Empty;
    public string ConnectorAccessTokenHash { get; init; } = string.Empty;
    public DateTime RegisteredAtUtc { get; init; }
}
