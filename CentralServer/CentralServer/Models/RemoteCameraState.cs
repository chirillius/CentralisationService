namespace CentralServer.Models;

public sealed class RemoteCameraState
{
    public required string CompanyKey { get; init; }

    public required string SiteKey { get; init; }

    public required string SiteName { get; init; }

    public required string CameraKey { get; init; }

    public required string SourceCameraKey { get; init; }

    public int? CameraId { get; init; }

    public required string CameraName { get; init; }

    public required string ServerBaseUrl { get; set; }

    public string ConnectorAccessToken { get; init; } = string.Empty;

    public DateTime LastSyncUtc { get; set; }

    public bool IsAvailable { get; set; }
}
