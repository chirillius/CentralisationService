namespace Server.Models;

public sealed class RecordingOptions
{
    public string OutputRootPath { get; init; } = "recordings";

    public int MaxRecordingSeconds { get; init; } = 600;

    public string DefaultStreamQuality { get; init; } = "High";
}
