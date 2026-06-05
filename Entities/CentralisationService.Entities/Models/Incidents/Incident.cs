namespace CentralisationService.Entities.Models.Incidents;

public sealed class Incident
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid SiteId { get; init; }
    public Guid CameraId { get; init; }
    public Guid DetectionProfileId { get; init; }
    public string DetectionTypeKey { get; init; } = string.Empty;
    public IncidentStatus Status { get; init; }
    public DateTime OpenedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public IReadOnlyList<string> EvidenceRelativePaths { get; init; } = Array.Empty<string>();
}
