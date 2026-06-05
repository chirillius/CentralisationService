namespace CentralServer.Models;

public sealed class ActivateInvitationRequest
{
    public string InvitationToken { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Login { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class PlatformAdminLoginRequest
{
    public string Login { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class CreateCompanyRequest
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? AccessExpiresAtUtc { get; init; }
}

public sealed class CreateInvitationRequest
{
    public string Name { get; init; } = string.Empty;
    public string RoleKey { get; init; } = "company-operator";
    public IReadOnlyList<string> Permissions { get; init; } =
    [
        "sites.read",
        "cameras.read",
        "zones.manage",
        "detection-profiles.manage",
        "archive.read",
    ];
    public DateTime? ExpiresAtUtc { get; init; }
}

public sealed class BindCompanyServerRequest
{
    public string ServerAddress { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public int CleaningDay { get; init; }
}

public sealed class ConnectorRegistrationRequest
{
    public Guid CompanyId { get; init; }
    public string CompanyKey { get; init; } = string.Empty;
    public string SiteKey { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public string CentralServerUrl { get; init; } = string.Empty;
    public string ConnectorAccessToken { get; init; } = string.Empty;
}

public sealed class UpdateCompanyAccessRequest
{
    public string Status { get; init; } = "active";
    public DateTime? AccessExpiresAtUtc { get; init; }
    public string? Reason { get; init; }
}
