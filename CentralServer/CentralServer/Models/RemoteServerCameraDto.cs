namespace CentralServer.Models;

public sealed class RemoteServerCameraDto
{
    public int? Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public string HighQualityPath { get; init; } = "/Streaming/Channels/101";

    public string LowQualityPath { get; init; } = "/Streaming/Channels/102";
}
