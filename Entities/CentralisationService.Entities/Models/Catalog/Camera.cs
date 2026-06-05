namespace CentralisationService.Entities.Models.Catalog;

public sealed class Camera
{
    public Guid Id { get; init; }
    public Guid SiteId { get; init; }
    public Guid ServerNodeId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string? StreamAddress { get; init; }
    public string? AnalyticsStreamAddress { get; init; }
    public bool IsEnabled { get; init; } = true;
}
