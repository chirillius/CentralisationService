using Microsoft.Extensions.Options;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class MotionFrameArchiveService
{
    private readonly MotionMonitoringOptions _options;
    private readonly ILogger<MotionFrameArchiveService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly MotionFrameIndexService _indexService;

    public MotionFrameArchiveService(
        IOptions<MotionMonitoringOptions> options,
        ILogger<MotionFrameArchiveService> logger,
        IWebHostEnvironment environment,
        MotionFrameIndexService indexService)
    {
        _options = options.Value;
        _logger = logger;
        _environment = environment;
        _indexService = indexService;
    }

    public async Task<string> SaveFrameAsync(RemoteCameraState camera, byte[] frameBytes, CancellationToken cancellationToken)
    {
        var dateFolderName = DateTime.Now.ToString("yyyy-MM-dd");
        var cameraFolderName = SanitizeFilePart(camera.CameraName);
        var directoryPath = Path.Combine(_environment.ContentRootPath, _options.VideosRootPath, dateFolderName, cameraFolderName);

        Directory.CreateDirectory(directoryPath);

        var fileName = $"{DateTime.Now:HH-mm-ss-fff}.jpg";
        var fullPath = Path.Combine(directoryPath, fileName);
        var relativePath = Path.Combine(dateFolderName, cameraFolderName, fileName).Replace("\\", "/");

        await File.WriteAllBytesAsync(fullPath, frameBytes, cancellationToken);
        _logger.LogInformation("Saved motion frame to {Path}", fullPath);

        _indexService.Add(new MotionFrameRecord
        {
            CameraKey = camera.CameraKey,
            CameraName = camera.CameraName,
            SiteKey = camera.SiteKey,
            SiteName = camera.SiteName,
            RelativePath = relativePath,
            FileName = fileName,
            PublicUrl = $"/api/archive/frame/{Uri.EscapeDataString(relativePath)}",
            CapturedAtUtc = DateTime.UtcNow,
        });

        return fullPath;
    }

    private static string SanitizeFilePart(string raw)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(raw.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "camera" : sanitized.Trim();
    }
}
