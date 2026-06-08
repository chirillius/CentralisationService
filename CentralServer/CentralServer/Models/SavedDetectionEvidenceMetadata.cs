namespace CentralServer.Models;

public sealed class SavedDetectionEvidenceMetadata
{
    public string CompanyKey { get; init; } = string.Empty;
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
    public IReadOnlyList<SavedDetectionObjectMetadata> Objects { get; init; } = Array.Empty<SavedDetectionObjectMetadata>();
}

public sealed class SavedDetectionObjectMetadata
{
    public string DetectionTypeKey { get; init; } = string.Empty;
    public string? Label { get; init; }
    public double? Confidence { get; init; }
    public SavedDetectionBoundsMetadata Bounds { get; init; } = new();
}

public sealed class SavedDetectionBoundsMetadata
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}
