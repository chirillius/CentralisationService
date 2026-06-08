using Server.Models;

namespace Server.Services;

public sealed class CameraRtspAddressService
{
    private readonly CameraSecretsService _secretsService;

    public CameraRtspAddressService(CameraSecretsService secretsService)
    {
        _secretsService = secretsService;
    }

    public async Task<string> ResolveAddressAsync(
        CameraSource camera,
        CameraStreamQuality quality,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(camera.Address)
            && !string.IsNullOrWhiteSpace(camera.StreamAddress))
        {
            return quality == CameraStreamQuality.High ? camera.Address : camera.StreamAddress;
        }

        if (!string.IsNullOrWhiteSpace(camera.Address) && string.IsNullOrWhiteSpace(camera.Host))
        {
            return camera.ResolveCaptureAddress();
        }

        var host = camera.ResolveHost();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException($"Для камеры '{camera.Name}' не указан адрес host.");
        }

        var credentials = await _secretsService.GetCredentialsAsync(camera, cancellationToken);
        if (credentials is null
            || string.IsNullOrWhiteSpace(credentials.Username)
            || string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new InvalidOperationException($"Для камеры '{camera.Name}' не настроены локальные учетные данные.");
        }

        var path = quality == CameraStreamQuality.High ? camera.HighQualityPath : camera.LowQualityPath;
        path = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (!path.StartsWith('/'))
        {
            path = $"/{path}";
        }

        var username = Uri.EscapeDataString(credentials.Username.Trim());
        var password = Uri.EscapeDataString(credentials.Password);
        return $"rtsp://{username}:{password}@{host.Trim()}{path}";
    }
}
