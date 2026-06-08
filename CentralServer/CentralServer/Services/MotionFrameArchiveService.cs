using Microsoft.Extensions.Options;
using CentralServer.Models;
using System.Diagnostics;

namespace CentralServer.Services;

public sealed class MotionFrameArchiveService
{
    private readonly MotionMonitoringOptions _options;
    private readonly ILogger<MotionFrameArchiveService> _logger;
    private readonly MotionFrameIndexService _indexService;
    private readonly CentralArchivePathService _pathService;

    public MotionFrameArchiveService(
        IOptions<MotionMonitoringOptions> options,
        ILogger<MotionFrameArchiveService> logger,
        MotionFrameIndexService indexService,
        CentralArchivePathService pathService)
    {
        _options = options.Value;
        _logger = logger;
        _indexService = indexService;
        _pathService = pathService;
    }

    public async Task<string> SaveVideoFragmentAsync(
        RemoteCameraState camera,
        byte[] firstFrameBytes,
        Func<CancellationToken, Task<byte[]>> frameProvider,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var startedAtLocal = DateTime.Now;
        var directoryPath = _pathService.BuildMotionVideoDirectory(_options.VideosRootPath, camera, startedAtLocal);
        Directory.CreateDirectory(directoryPath);

        var tempDirectory = Path.Combine(directoryPath, $".tmp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var frameInterval = TimeSpan.FromMilliseconds(Math.Max(100, _options.VideoFrameIntervalMilliseconds));
            var duration = TimeSpan.FromSeconds(Math.Max(1, _options.VideoFragmentSeconds));
            var frameIndex = 1;

            await SaveSequenceFrameAsync(tempDirectory, frameIndex++, firstFrameBytes, cancellationToken);

            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(frameInterval, cancellationToken);
                var frameBytes = await frameProvider(cancellationToken);
                await SaveSequenceFrameAsync(tempDirectory, frameIndex++, frameBytes, cancellationToken);
            }

            var endedAtLocal = DateTime.Now;
            var fileName = $"{startedAtLocal:HH-mm-ss.fff}_{endedAtLocal:HH-mm-ss.fff}.mp4";
            var fullPath = Path.Combine(directoryPath, fileName);
            await EncodeFramesAsync(tempDirectory, fullPath, frameInterval, cancellationToken);

            var relativePath = _pathService.ToRelativePath(_options.VideosRootPath, fullPath);

            _logger.LogInformation("Saved motion video fragment to {Path}", fullPath);

            _indexService.Add(new MotionFrameRecord
            {
                CameraKey = camera.CameraKey,
                CameraName = camera.CameraName,
                CompanyKey = camera.CompanyKey,
                SiteKey = camera.SiteKey,
                SiteName = camera.SiteName,
                RelativePath = relativePath,
                FileName = fileName,
                PublicUrl = $"/api/archive/frame/{Uri.EscapeDataString(relativePath)}",
                CapturedAtUtc = startedAtUtc,
            });

            return fullPath;
        }
        finally
        {
            TryDeleteTempDirectory(tempDirectory);
        }
    }

    private static async Task SaveSequenceFrameAsync(
        string tempDirectory,
        int frameIndex,
        byte[] frameBytes,
        CancellationToken cancellationToken)
    {
        var framePath = Path.Combine(tempDirectory, $"frame_{frameIndex:000000}.jpg");
        await File.WriteAllBytesAsync(framePath, frameBytes, cancellationToken);
    }

    private async Task EncodeFramesAsync(
        string tempDirectory,
        string outputPath,
        TimeSpan frameInterval,
        CancellationToken cancellationToken)
    {
        var framesPerSecond = Math.Clamp(1000d / Math.Max(100, frameInterval.TotalMilliseconds), 1d, 30d);
        var inputPattern = Path.Combine(tempDirectory, "frame_%06d.jpg");
        var arguments = string.Join(
            ' ',
            "-y",
            "-framerate",
            framesPerSecond.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-i",
            Quote(inputPattern),
            "-c:v libx264",
            "-pix_fmt yuv420p",
            "-movflags +faststart",
            Quote(outputPath));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        process.Start();
        var errorOutputTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorOutput = await errorOutputTask;
            throw new InvalidOperationException($"ffmpeg не смог создать видеофрагмент: {errorOutput}");
        }
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private void TryDeleteTempDirectory(string tempDirectory)
    {
        try
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete temporary motion video directory {TempDirectory}", tempDirectory);
        }
    }
}
