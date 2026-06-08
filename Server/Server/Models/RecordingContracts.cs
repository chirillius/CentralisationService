namespace Server.Models;

public sealed class StartCameraRecordingRequest
{
    public string StreamQuality { get; init; } = "High";
    public int? MaxRecordingSeconds { get; init; }
}

public sealed class CameraRecordingResponse
{
    public required string RecordingId { get; init; }
    public required string CameraKey { get; init; }
    public required string CameraName { get; init; }
    public required DateTime StartedAtUtc { get; init; }
    public DateTime? StoppedAtUtc { get; init; }
    public required bool IsRunning { get; init; }
    public string? FileName { get; init; }
}
