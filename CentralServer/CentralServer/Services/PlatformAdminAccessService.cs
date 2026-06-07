using System.Security.Cryptography;
using System.Text;
using CentralServer.Models;
using Microsoft.Extensions.Options;

namespace CentralServer.Services;

public sealed class PlatformAdminAccessService
{
    private readonly AccessStoreService _accessStoreService;
    private readonly AccessOptions _options;

    public PlatformAdminAccessService(AccessStoreService accessStoreService, IOptions<AccessOptions> options)
    {
        _accessStoreService = accessStoreService;
        _options = options.Value;
    }

    public async Task<bool> IsPlatformAdminAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var bearerToken = GetBearerToken(context);
        if (!string.IsNullOrWhiteSpace(bearerToken)
            && await _accessStoreService.ResolvePlatformAdminSessionAsync(bearerToken, cancellationToken) is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_options.PlatformAdminKey))
        {
            return false;
        }

        var provided = context.Request.Headers["X-Platform-Admin-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(_options.PlatformAdminKey)));
    }

    private static string? GetBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
