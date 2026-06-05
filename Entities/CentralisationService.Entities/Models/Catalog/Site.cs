namespace CentralisationService.Entities.Models.Catalog;

public sealed class Site
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int CleaningDay { get; init; }
    public bool IsEnabled { get; init; } = true;
}
