namespace CentralServer.Models;

public sealed class MotionMonitoringOptions
{
    public int PollIntervalMilliseconds { get; init; } = 2500;

    public double MotionThreshold { get; init; } = 14.0d;

    public int SaveCooldownSeconds { get; init; } = 8;

    public string VideosRootPath { get; init; } = "videos";

    public int ThumbnailWidth { get; init; } = 64;

    public int ThumbnailHeight { get; init; } = 36;
}
