namespace CentralServer.Models;

public sealed class ConfiguredRetailDetectionProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProfileKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public string DetectionTypeKey { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public string ClientZoneTypeKey { get; init; } = "client-zone";
    public string? TargetZoneTypeKey { get; init; }
    public bool RequiresClientZonePresence { get; init; } = true;
    public bool SaveEvidenceOnPositiveResult { get; init; } = true;
    public int IntervalSeconds { get; init; } = 5;
    public int CooldownSeconds { get; init; } = 20;
    public double ConfidenceThreshold { get; init; } = 0.25;
}
