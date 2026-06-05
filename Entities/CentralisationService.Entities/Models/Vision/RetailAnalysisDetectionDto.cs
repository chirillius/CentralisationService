using CentralisationService.Entities.Models.Zones;

namespace CentralisationService.Entities.Models.Vision;

public sealed class RetailAnalysisDetectionDto
{
    public string DetectionTypeKey { get; init; } = string.Empty;
    public bool IsDetected { get; init; }
    public double? Confidence { get; init; }
    public string? EvidenceLabel { get; init; }
    public IReadOnlyList<ZoneBoundsDto> BoundingBoxes { get; init; } = Array.Empty<ZoneBoundsDto>();
}
