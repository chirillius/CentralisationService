namespace CentralServer.Models;

public sealed class RegisteredServerState
{
    public required string CompanyKey { get; init; }

    public required string SiteKey { get; init; }

    public required string SiteName { get; set; }

    public required string ServerBaseUrl { get; init; }

    public string ConnectorId { get; set; } = string.Empty;

    public string ConnectorAccessToken { get; set; } = string.Empty;

    public int CleaningDay { get; init; }

    public DateTime LastSyncUtc { get; set; }

    public bool IsAvailable { get; set; }

    public List<RemoteCameraState> Cameras { get; } = [];
}
