namespace CentralServer.Models;

public sealed class MotionMonitoringOptions
{
    public int PollIntervalMilliseconds { get; init; } = 2500;

    public double MotionThreshold { get; init; } = 14.0d;

    public double ContinueMotionThreshold { get; init; } = 6.0d;

    public string VideosRootPath { get; init; } = "company";

    public string FfmpegPath { get; init; } = "ffmpeg";

    public int VideoFragmentSeconds { get; init; } = 12;

    public int VideoFrameIntervalMilliseconds { get; init; } = 1000;

    public int StopAfterNoMotionSeconds { get; init; } = 60;

    public int MinRecordingSeconds { get; init; } = 60;

    public int MaxRecordingMinutes { get; init; } = 10;

    public string RecordingStreamQuality { get; init; } = "High";

    public int ThumbnailWidth { get; init; } = 64;

    public int ThumbnailHeight { get; init; } = 36;
}
