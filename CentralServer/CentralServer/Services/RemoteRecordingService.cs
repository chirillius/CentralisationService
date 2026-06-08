using CentralServer.Models;
using System.Net.Http.Json;

namespace CentralServer.Services;

public sealed class RemoteRecordingService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RemoteRecordingService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RemoteRecordingDto> StartAsync(
        RemoteCameraState camera,
        MotionMonitoringOptions options,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(camera);
        using var response = await client.PostAsJsonAsync(
            $"{camera.ServerBaseUrl.TrimEnd('/')}/api/cameras/{Uri.EscapeDataString(camera.SourceCameraKey)}/recordings/start",
            new StartRemoteRecordingRequest
            {
                StreamQuality = options.RecordingStreamQuality,
                MaxRecordingSeconds = options.MaxRecordingMinutes * 60,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteRecordingDto>(cancellationToken)
            ?? throw new InvalidOperationException("Server returned empty recording response.");
    }

    public async Task<RemoteRecordingDto?> StopAsync(
        RemoteCameraState camera,
        string recordingId,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(camera);
        using var response = await client.PostAsync(
            $"{camera.ServerBaseUrl.TrimEnd('/')}/api/cameras/{Uri.EscapeDataString(camera.SourceCameraKey)}/recordings/stop",
            content: null,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteRecordingDto>(cancellationToken);
    }

    public async Task<byte[]> DownloadAsync(
        RemoteCameraState camera,
        string recordingId,
        CancellationToken cancellationToken)
    {
        var client = CreateClient(camera);
        using var response = await client.GetAsync(
            $"{camera.ServerBaseUrl.TrimEnd('/')}/api/cameras/{Uri.EscapeDataString(camera.SourceCameraKey)}/recordings/{Uri.EscapeDataString(recordingId)}/download",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private HttpClient CreateClient(RemoteCameraState camera)
    {
        if (string.IsNullOrWhiteSpace(camera.ServerBaseUrl))
        {
            throw new InvalidOperationException($"Camera '{camera.CameraName}' has no active server base URL.");
        }

        var client = _httpClientFactory.CreateClient(nameof(RemoteRecordingService));
        if (!string.IsNullOrWhiteSpace(camera.ConnectorAccessToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Connector-Token", camera.ConnectorAccessToken);
        }

        return client;
    }
}
