using CentralisationService.Entities.Models.Defects;

namespace Neuro.Services;

public sealed class DefectCatalogService
{
    private static readonly DefectDescriptor[] Defects =
    [
        new() { Key = "phone", Name = "Телефон", Category = "behavior", DetectionKind = "object-person-relation" },
        new() { Key = "bottles", Name = "Бутылки", Category = "cleanliness", DetectionKind = "object-person-relation" },
        new() { Key = "smoke", Name = "Дым", Category = "safety", DetectionKind = "visual-smoke" },
        new() { Key = "mopping", Name = "Швабра", Category = "cleanliness", DetectionKind = "object-person-relation" },
        new() { Key = "cash-register", Name = "Касса открыта/закрыта", Category = "cashier", DetectionKind = "classification" },
        new() { Key = "pose", Name = "Сидит при клиенте", Category = "behavior", DetectionKind = "classification" },
        new() { Key = "conversion", Name = "Подсчет входов", Category = "traffic", DetectionKind = "tracking" },
        new() { Key = "clear-stall", Name = "Лишние предметы на прилавке", Category = "cleanliness", DetectionKind = "surface-analysis" },
        new() { Key = "badge", Name = "Бейдж", Category = "uniform", DetectionKind = "classification" },
        new() { Key = "clothes", Name = "Форма", Category = "uniform", DetectionKind = "classification" }
    ];

    public IReadOnlyList<DefectDescriptor> GetAll() => Defects;
}
