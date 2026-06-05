namespace CentralisationService.Entities.Models.Catalog;

public sealed class DetectionProfile
{
    public Guid Id { get; init; }
    public Guid SiteId { get; init; }
    public Guid CameraId { get; init; }
    public Guid ZoneId { get; init; }
    public Guid DetectionTypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public int IntervalSeconds { get; init; }
    public int CooldownSeconds { get; init; }
    public double? Threshold { get; init; }
    public DetectionSchedule Schedule { get; init; } = new();
}
