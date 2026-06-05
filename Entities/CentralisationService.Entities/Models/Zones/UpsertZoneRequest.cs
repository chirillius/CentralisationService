namespace CentralisationService.Entities.Models.Zones;

public sealed class UpsertZoneRequest
{
    public Guid? Id { get; init; }
    public string SiteKey { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public string ZoneTypeKey { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public string? CustomName { get; init; }
    public IReadOnlyList<ZonePointDto> Points { get; init; } = Array.Empty<ZonePointDto>();
}
