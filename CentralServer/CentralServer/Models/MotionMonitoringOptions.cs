namespace CentralServer.Models;

public sealed class MotionMonitoringOptions
{
    public int PollIntervalMilliseconds { get; init; } = 2500;

    public double MotionThreshold { get; init; } = 14.0d;

    public int SaveCooldownSeconds { get; init; } = 8;

    public string VideosRootPath { get; init; } = "company";

    public string FfmpegPath { get; init; } = "ffmpeg";

    public int VideoFragmentSeconds { get; init; } = 12;

    public int VideoFrameIntervalMilliseconds { get; init; } = 1000;

    public int ThumbnailWidth { get; init; } = 64;

    public int ThumbnailHeight { get; init; } = 36;
}
