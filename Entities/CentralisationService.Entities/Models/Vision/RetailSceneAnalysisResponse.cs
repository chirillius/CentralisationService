namespace CentralisationService.Entities.Models.Vision;

public sealed class RetailSceneAnalysisResponse
{
    public bool ClientZoneHasPeople { get; init; }
    public double? ClientZoneConfidence { get; init; }
    public bool IsSimulated { get; init; }
    public string Note { get; init; } = string.Empty;
    public IReadOnlyList<RetailAnalysisDetectionDto> Detections { get; init; } = Array.Empty<RetailAnalysisDetectionDto>();
}
