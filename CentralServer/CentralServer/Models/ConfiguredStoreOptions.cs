namespace CentralServer.Models;

public sealed class ConfiguredStoreOptions
{
    public required string CompanyKey { get; init; }

    public required string SiteKey { get; init; }

    public required string SiteName { get; init; }

    public required string ServerBaseUrl { get; init; }

    public int CleaningDay { get; init; }

    public string ConnectorAccessToken { get; init; } = string.Empty;
}
