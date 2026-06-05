namespace Server.Models;

public sealed class ServerNodeOptions
{
    public string ConnectorId { get; init; } = "site-connector-001";

    public string SiteName { get; init; } = "Demo Site";

    public string SiteKey { get; init; } = "demo-site";

    public string FfmpegPath { get; init; } = "ffmpeg";

    public List<CameraSource> Cameras { get; init; } = [];
}
