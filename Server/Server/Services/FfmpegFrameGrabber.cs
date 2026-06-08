using System.Diagnostics;
using Microsoft.Extensions.Options;
using Server.Models;

namespace Server.Services;

public sealed class FfmpegFrameGrabber
{
    private readonly ServerNodeOptions _options;
    private readonly ILogger<FfmpegFrameGrabber> _logger;
    private readonly CameraRtspAddressService _rtspAddressService;

    public FfmpegFrameGrabber(
        IOptions<ServerNodeOptions> options,
        ILogger<FfmpegFrameGrabber> logger,
        CameraRtspAddressService rtspAddressService)
    {
        _options = options.Value;
        _logger = logger;
        _rtspAddressService = rtspAddressService;
    }

    public async Task<byte[]> CaptureFrameAsync(CameraSource camera, CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"centralisation-frame-{Guid.NewGuid():N}.jpg");

        try
        {
            var captureAddress = await _rtspAddressService.ResolveAddressAsync(camera, CameraStreamQuality.Low, cancellationToken);
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments =
                    $"-y -rtsp_transport tcp -i \"{captureAddress}\" -frames:v 1 -q:v 2 \"{tempFilePath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stderr = await stderrTask;
            _ = await stdoutTask;

            if (process.ExitCode != 0 || !File.Exists(tempFilePath))
            {
                throw new InvalidOperationException(
                    $"ffmpeg не смог получить кадр камеры '{camera.Name}'. {SanitizeFfmpegOutput(stderr)}".Trim());
            }

            return await File.ReadAllBytesAsync(tempFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture frame for camera {CameraName}", camera.Name);
            throw;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static string SanitizeFfmpegOutput(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"rtsp://([^:\s/]+):([^@\s/]+)@",
            "rtsp://***:***@",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
