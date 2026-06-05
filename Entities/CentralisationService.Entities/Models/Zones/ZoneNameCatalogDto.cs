namespace CentralisationService.Entities.Models.Zones;

public sealed class ZoneNameCatalogDto
{
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();
    public bool AllowCustom { get; init; }
    public string CustomOptionLabel { get; init; } = string.Empty;
}
