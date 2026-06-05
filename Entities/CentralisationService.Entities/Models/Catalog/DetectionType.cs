namespace CentralisationService.Entities.Models.Catalog;

public sealed class DetectionType
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DetectionKind { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
