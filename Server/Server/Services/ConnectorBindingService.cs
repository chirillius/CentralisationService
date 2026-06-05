using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server.Models;

namespace Server.Services;

public sealed class ConnectorBindingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ConnectorBindingService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ConnectorBindingRecord?> GetBindingAsync(CancellationToken cancellationToken)
    {
        var path = EnsurePath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ConnectorBindingRecord>(stream, JsonOptions, cancellationToken);
    }

    public async Task<ConnectorBindingRecord> RegisterAsync(ConnectorRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.CompanyKey)
            || string.IsNullOrWhiteSpace(request.SiteKey)
            || string.IsNullOrWhiteSpace(request.ConnectorAccessToken))
        {
            throw new InvalidOperationException("Нужно указать компанию, точку и токен доступа коннектора.");
        }

        var binding = new ConnectorBindingRecord
        {
            CompanyId = request.CompanyId,
            CompanyKey = request.CompanyKey.Trim(),
            SiteKey = request.SiteKey.Trim(),
            SiteName = string.IsNullOrWhiteSpace(request.SiteName) ? request.SiteKey.Trim() : request.SiteName.Trim(),
            CentralServerUrl = request.CentralServerUrl.Trim().TrimEnd('/'),
            ConnectorAccessTokenHash = HashToken(request.ConnectorAccessToken.Trim()),
            RegisteredAtUtc = DateTime.UtcNow,
        };

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var path = EnsurePath();
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, binding, JsonOptions, cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }

        return binding;
    }

    public async Task<bool> IsRequestAuthorizedAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var binding = await GetBindingAsync(cancellationToken);
        if (binding is null)
        {
            return true;
        }

        var providedToken = request.Headers["X-Connector-Token"].ToString();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashToken(providedToken.Trim())),
            Convert.FromHexString(binding.ConnectorAccessTokenHash));
    }

    private string EnsurePath()
    {
        var root = Path.Combine(_environment.ContentRootPath, "Configuration");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "connector-binding.json");
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
