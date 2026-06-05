namespace CentralServer.Models;

public sealed class SavedDetectionEvidenceMetadata
{
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public string CameraName { get; init; } = string.Empty;
    public string ProfileKey { get; init; } = string.Empty;
    public string DetectionTypeKey { get; init; } = string.Empty;
    public DateTime CapturedAtUtc { get; init; }
    public bool ClientZoneHasPeople { get; init; }
    public bool IsSimulated { get; init; }
    public string Note { get; init; } = string.Empty;
}
