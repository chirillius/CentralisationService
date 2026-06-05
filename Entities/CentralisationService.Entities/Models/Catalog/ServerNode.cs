namespace CentralisationService.Entities.Models.Catalog;

public sealed class ServerNode
{
    public Guid Id { get; init; }
    public Guid SiteId { get; init; }
    public string ConnectorId { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string? PublicAddress { get; init; }
    public bool IsEnabled { get; init; } = true;
}
