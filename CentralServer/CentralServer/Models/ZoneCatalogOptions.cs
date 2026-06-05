namespace CentralServer.Models;

public sealed class ZoneCatalogOptions
{
    public string ConfigurationDirectory { get; set; } = "Configuration";
    public string ZoneNamesFileName { get; set; } = "zone_names.json";
    public string ZonesFileName { get; set; } = "zones.json";
    public string CustomOptionLabel { get; set; } = "Свой вариант";
}
