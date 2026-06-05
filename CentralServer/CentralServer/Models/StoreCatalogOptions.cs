namespace CentralServer.Models;

public sealed class StoreCatalogOptions
{
    public int RefreshIntervalSeconds { get; init; } = 20;

    public List<ConfiguredStoreOptions> Stores { get; init; } = [];
}
