namespace CentralServer.Models;

public sealed class StartRemoteRecordingRequest
{
    public string StreamQuality { get; init; } = "High";
    public int? MaxRecordingSeconds { get; init; }
}

public sealed class RemoteRecordingDto
{
    public string RecordingId { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public string CameraName { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? StoppedAtUtc { get; init; }
    public bool IsRunning { get; init; }
    public string? FileName { get; init; }
}
