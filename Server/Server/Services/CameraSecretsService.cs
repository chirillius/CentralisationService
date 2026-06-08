using System.Text.Json;
using Server.Models;

namespace Server.Services;

public sealed class CameraSecretsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IWebHostEnvironment _environment;

    public CameraSecretsService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<CameraCredentialRecord?> GetCredentialsAsync(CameraSource camera, CancellationToken cancellationToken)
    {
        var path = GetSecretsPath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var secrets = await JsonSerializer.DeserializeAsync<CameraSecretsConfiguration>(stream, JsonOptions, cancellationToken)
            ?? new CameraSecretsConfiguration();
        var host = camera.ResolveHost();

        return !string.IsNullOrWhiteSpace(host) && secrets.Cameras.TryGetValue(host, out var cameraCredentials)
            ? cameraCredentials
            : secrets.Default;
    }

    public string GetSecretsPath()
    {
        var root = Path.Combine(_environment.ContentRootPath, "Configuration");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "camera-secrets.json");
    }
}
