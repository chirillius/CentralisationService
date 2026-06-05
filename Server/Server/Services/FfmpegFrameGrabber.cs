using System.Diagnostics;
using Microsoft.Extensions.Options;
using Server.Models;

namespace Server.Services;

public sealed class FfmpegFrameGrabber
{
    private readonly ServerNodeOptions _options;
    private readonly ILogger<FfmpegFrameGrabber> _logger;

    public FfmpegFrameGrabber(IOptions<ServerNodeOptions> options, ILogger<FfmpegFrameGrabber> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> CaptureFrameAsync(CameraSource camera, CancellationToken cancellationToken)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"centralisation-frame-{Guid.NewGuid():N}.jpg");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments =
                    $"-y -rtsp_transport tcp -i \"{camera.ResolveCaptureAddress()}\" -frames:v 1 -q:v 2 \"{tempFilePath}\"",
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
                    $"ffmpeg could not capture a frame for camera '{camera.Name}'. {stderr}".Trim());
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
}
