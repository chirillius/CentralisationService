namespace CentralisationService.Entities.Models.Catalog;

public sealed class Company
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}
