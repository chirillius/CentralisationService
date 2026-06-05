using CentralServer.Models;

namespace CentralServer.Services;

public sealed class RemoteFrameProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RemoteFrameProxyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<byte[]> GetFrameAsync(RemoteCameraState camera, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(camera.ServerBaseUrl))
        {
            throw new InvalidOperationException($"Camera '{camera.CameraName}' has no active server base URL.");
        }

        var client = _httpClientFactory.CreateClient(nameof(RemoteFrameProxyService));
        if (!string.IsNullOrWhiteSpace(camera.ConnectorAccessToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Connector-Token", camera.ConnectorAccessToken);
        }

        using var response = await client.GetAsync(
            $"{camera.ServerBaseUrl}/api/cameras/{Uri.EscapeDataString(camera.SourceCameraKey)}/frame",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
