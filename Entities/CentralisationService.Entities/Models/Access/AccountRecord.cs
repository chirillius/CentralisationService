namespace CentralisationService.Entities.Models.Access;

public sealed class AccountRecord
{
    public Guid Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string PasswordSalt { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; }
}
