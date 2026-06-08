using CentralServer.Models;

namespace CentralServer.Services;

public sealed class CompanyAccessContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CompanyAccessContextService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public AuthenticatedCompanyContext RequireCurrent()
    {
        return _httpContextAccessor.HttpContext?.Items[CompanyAccessMiddleware.ContextItemKey] as AuthenticatedCompanyContext
            ?? throw new AccessDeniedException("authentication_required", "Authentication is required.");
    }

    public bool CanAccessSite(string siteKey)
    {
        var context = RequireCurrent();
        return siteKey.StartsWith($"{context.CompanyKey}-", StringComparison.OrdinalIgnoreCase)
            || string.Equals(siteKey, context.CompanyKey, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasPermission(string permission)
    {
        var context = RequireCurrent();
        return context.Permissions.Contains(permission);
    }
}
