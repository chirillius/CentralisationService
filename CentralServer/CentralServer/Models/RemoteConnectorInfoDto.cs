namespace CentralServer.Models;

public sealed class RemoteConnectorInfoDto
{
    public string ConnectorId { get; init; } = string.Empty;

    public string SiteKey { get; init; } = string.Empty;

    public string SiteName { get; init; } = string.Empty;

    public int CameraCount { get; init; }
}
