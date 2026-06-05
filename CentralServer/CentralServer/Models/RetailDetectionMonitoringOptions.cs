namespace CentralServer.Models;

public sealed class RetailDetectionMonitoringOptions
{
    public int LoopDelayMilliseconds { get; set; } = 3000;
    public int NeuroTimeoutSeconds { get; set; } = 15;
    public string NeuroBaseUrl { get; set; } = "http://localhost:5300";
    public string ProfilesFileName { get; set; } = "detection_profiles.json";
    public string VideosRootPath { get; set; } = "videos";
}
