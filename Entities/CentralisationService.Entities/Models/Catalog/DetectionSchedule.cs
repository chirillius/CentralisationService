namespace CentralisationService.Entities.Models.Catalog;

public sealed class DetectionSchedule
{
    public string TimeZone { get; init; } = "UTC";
    public TimeOnly? ActiveFromLocalTime { get; init; }
    public TimeOnly? ActiveToLocalTime { get; init; }
    public IReadOnlyList<DayOfWeek> ActiveDays { get; init; } = Array.Empty<DayOfWeek>();
}
