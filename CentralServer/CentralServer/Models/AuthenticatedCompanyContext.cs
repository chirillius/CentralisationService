namespace CentralServer.Models;

public sealed class AuthenticatedCompanyContext
{
    public required Guid CompanyId { get; init; }
    public required string CompanyKey { get; init; }
    public required string CompanyName { get; init; }
    public required Guid AccountId { get; init; }
    public required string Login { get; init; }
    public required string DisplayName { get; init; }
    public required string RoleKey { get; init; }
    public required IReadOnlySet<string> Permissions { get; init; }
    public required Guid SessionId { get; init; }
    public DateTime? AccessExpiresAtUtc { get; init; }
}
