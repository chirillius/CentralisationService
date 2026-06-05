namespace CentralisationService.Entities.Models.Zones;

public sealed class ZoneRecord
{
    public Guid Id { get; init; }
    public string SiteKey { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public string ZoneTypeKey { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public string? CustomName { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<ZonePointDto> Points { get; init; } = Array.Empty<ZonePointDto>();
    public ZoneBoundsDto Bounds { get; init; } = new();
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
