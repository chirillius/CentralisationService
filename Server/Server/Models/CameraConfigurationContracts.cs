namespace Server.Models;

public sealed class CameraConfigurationRequest
{
    public int? Id { get; init; }
    public string? Key { get; init; }
    public required string Name { get; init; }
    public required string Host { get; init; }
    public string HighQualityPath { get; init; } = "/Streaming/Channels/101";
    public string LowQualityPath { get; init; } = "/Streaming/Channels/102";
}

public sealed class CameraSecretsConfiguration
{
    public CameraCredentialRecord? Default { get; init; }
    public Dictionary<string, CameraCredentialRecord> Cameras { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CameraCredentialRecord
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public enum CameraStreamQuality
{
    High,
    Low,
}
