namespace CentralServer.Models;

public sealed class MotionFrameRecord
{
    public required string CameraKey { get; init; }

    public required string CameraName { get; init; }

    public required string SiteKey { get; init; }

    public required string SiteName { get; init; }

    public required string RelativePath { get; init; }

    public required string FileName { get; init; }

    public required string PublicUrl { get; init; }

    public DateTime CapturedAtUtc { get; init; }
}
