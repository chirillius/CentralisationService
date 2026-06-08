using System.Text.Json;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class RetailDetectionEvidenceArchiveService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly RetailDetectionMonitoringOptions _options;
    private readonly CentralArchivePathService _pathService;

    public RetailDetectionEvidenceArchiveService(
        Microsoft.Extensions.Options.IOptions<RetailDetectionMonitoringOptions> options,
        CentralArchivePathService pathService)
    {
        _options = options.Value;
        _pathService = pathService;
    }

    public async Task<string> SaveAsync(
        RemoteCameraState camera,
        string reasonKey,
        byte[] frameBytes,
        SavedDetectionEvidenceMetadata metadata,
        CancellationToken cancellationToken)
    {
        var capturedAt = metadata.CapturedAtUtc == default ? DateTime.UtcNow : metadata.CapturedAtUtc;
        var directory = _pathService.BuildDefectImagesDirectory(_options.VideosRootPath, camera, capturedAt, reasonKey);
        Directory.CreateDirectory(directory);

        var fileStem = $"{capturedAt:HH-mm-ss-fff}_{CentralArchivePathService.SanitizeSegment(camera.CameraKey)}";
        var imagePath = Path.Combine(directory, $"{fileStem}.jpg");
        var metadataPath = Path.Combine(directory, $"{fileStem}.json");

        await File.WriteAllBytesAsync(imagePath, frameBytes, cancellationToken);
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, SerializerOptions), cancellationToken);

        return _pathService.ToRelativePath(_options.VideosRootPath, imagePath);
    }
}
