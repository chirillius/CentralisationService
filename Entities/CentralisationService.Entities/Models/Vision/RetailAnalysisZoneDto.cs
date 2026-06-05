using CentralisationService.Entities.Models.Zones;

namespace CentralisationService.Entities.Models.Vision;

public sealed class RetailAnalysisZoneDto
{
    public string ZoneTypeKey { get; init; } = string.Empty;
    public string ZoneName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<ZonePointDto> Points { get; init; } = Array.Empty<ZonePointDto>();
    public ZoneBoundsDto Bounds { get; init; } = new();
}
