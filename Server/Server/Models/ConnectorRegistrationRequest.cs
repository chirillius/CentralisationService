namespace Server.Models;

public sealed class ConnectorRegistrationRequest
{
    public Guid CompanyId { get; init; }
    public string CompanyKey { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public string CentralServerUrl { get; init; } = string.Empty;
    public string ConnectorAccessToken { get; init; } = string.Empty;
}
