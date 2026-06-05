namespace CentralisationService.Entities.Models.Defects;

public sealed class DefectDescriptor
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string DetectionKind { get; init; }
}
