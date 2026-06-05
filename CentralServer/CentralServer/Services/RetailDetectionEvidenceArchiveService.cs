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
    private readonly IWebHostEnvironment _environment;

    public RetailDetectionEvidenceArchiveService(
        Microsoft.Extensions.Options.IOptions<RetailDetectionMonitoringOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<string> SaveAsync(
        RemoteCameraState camera,
        string reasonKey,
        byte[] frameBytes,
        SavedDetectionEvidenceMetadata metadata,
        CancellationToken cancellationToken)
    {
        var capturedAt = metadata.CapturedAtUtc == default ? DateTime.UtcNow : metadata.CapturedAtUtc;
        var root = Path.Combine(_environment.ContentRootPath, _options.VideosRootPath);
        var directory = Path.Combine(root, capturedAt.ToString("yyyy-MM-dd"), SanitizeSegment(camera.CameraName), "detections", SanitizeSegment(reasonKey));
        Directory.CreateDirectory(directory);

        var fileStem = $"{capturedAt:HH-mm-ss-fff}_{SanitizeSegment(camera.CameraKey)}";
        var imagePath = Path.Combine(directory, $"{fileStem}.jpg");
        var metadataPath = Path.Combine(directory, $"{fileStem}.json");

        await File.WriteAllBytesAsync(imagePath, frameBytes, cancellationToken);
        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, SerializerOptions), cancellationToken);

        return Path.GetRelativePath(_environment.ContentRootPath, imagePath).Replace('\\', '/');
    }

    private static string SanitizeSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
