namespace CentralisationService.Entities.Models.Vision;

public sealed class RetailSceneAnalysisRequest
{
    public string SiteKey { get; init; } = string.Empty;
    public string CameraKey { get; init; } = string.Empty;
    public byte[] FrameJpegBytes { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<RetailAnalysisZoneDto> Zones { get; init; } = Array.Empty<RetailAnalysisZoneDto>();
}
