using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Server.Models;

namespace Server.Services;

public sealed class CameraRecordingService
{
    private sealed class RecordingState
    {
        public required string RecordingId { get; init; }
        public required string CameraKey { get; init; }
        public required string CameraName { get; init; }
        public required string TemporaryPath { get; init; }
        public string? FinalPath { get; set; }
        public required DateTime StartedAtUtc { get; init; }
        public DateTime? StoppedAtUtc { get; set; }
        public required Process Process { get; init; }
    }

    private readonly ConcurrentDictionary<string, RecordingState> _activeByCamera = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RecordingState> _recordingsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly CameraConfigurationService _cameraConfigurationService;
    private readonly CameraRtspAddressService _rtspAddressService;
    private readonly ServerNodeOptions _serverOptions;
    private readonly RecordingOptions _recordingOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CameraRecordingService> _logger;

    public CameraRecordingService(
        CameraConfigurationService cameraConfigurationService,
        CameraRtspAddressService rtspAddressService,
        IOptions<ServerNodeOptions> serverOptions,
        IOptions<RecordingOptions> recordingOptions,
        IWebHostEnvironment environment,
        ILogger<CameraRecordingService> logger)
    {
        _cameraConfigurationService = cameraConfigurationService;
        _rtspAddressService = rtspAddressService;
        _serverOptions = serverOptions.Value;
        _recordingOptions = recordingOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<CameraRecordingResponse> StartAsync(
        string cameraKey,
        StartCameraRecordingRequest request,
        CancellationToken cancellationToken)
    {
        if (_activeByCamera.TryGetValue(cameraKey, out var active))
        {
            return ToResponse(active);
        }

        var camera = _cameraConfigurationService.GetCameras().FirstOrDefault(item =>
            string.Equals(item.ResolveKey(), cameraKey, StringComparison.OrdinalIgnoreCase));
        if (camera is null)
        {
            throw new InvalidOperationException($"Камера '{cameraKey}' не настроена.");
        }

        var quality = ResolveQuality(request.StreamQuality);
        var rtspAddress = await _rtspAddressService.ResolveAddressAsync(camera, quality, cancellationToken);
        var startedAtUtc = DateTime.UtcNow;
        var directory = BuildCameraDirectory(camera, startedAtUtc);
        Directory.CreateDirectory(directory);

        var recordingId = Guid.NewGuid().ToString("N");
        var temporaryPath = Path.Combine(directory, $"{recordingId}.tmp.mp4");
        var maxSeconds = Math.Clamp(request.MaxRecordingSeconds ?? _recordingOptions.MaxRecordingSeconds, 10, 24 * 60 * 60);
        var arguments = string.Join(
            ' ',
            "-y",
            "-rtsp_transport tcp",
            "-threads 1",
            "-t",
            maxSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-i",
            Quote(rtspAddress),
            "-c:v copy",
            "-an",
            Quote(temporaryPath));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _serverOptions.FfmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        var state = new RecordingState
        {
            RecordingId = recordingId,
            CameraKey = camera.ResolveKey(),
            CameraName = camera.Name,
            TemporaryPath = temporaryPath,
            StartedAtUtc = startedAtUtc,
            Process = process,
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data) && IsFfmpegError(eventArgs.Data))
            {
                _logger.LogWarning("ffmpeg recording warning for camera {CameraName}: {Line}", camera.Name, SanitizeFfmpegOutput(eventArgs.Data));
            }
        };
        process.Exited += (_, _) => FinalizeRecording(state);

        if (!_activeByCamera.TryAdd(camera.ResolveKey(), state))
        {
            process.Dispose();
            return ToResponse(_activeByCamera[camera.ResolveKey()]);
        }

        _recordingsById[recordingId] = state;
        process.Start();
        process.BeginErrorReadLine();
        _logger.LogInformation("Started recording {RecordingId} for camera {CameraName}", recordingId, camera.Name);
        return ToResponse(state);
    }

    public async Task<CameraRecordingResponse?> StopAsync(string cameraKey, CancellationToken cancellationToken)
    {
        if (!_activeByCamera.TryRemove(cameraKey, out var state))
        {
            return null;
        }

        try
        {
            if (!state.Process.HasExited)
            {
                await state.Process.StandardInput.WriteLineAsync("q");
                await state.Process.WaitForExitAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException)
        {
            // The process may have already exited by max-duration.
        }

        FinalizeRecording(state);
        return ToResponse(state);
    }

    public CameraRecordingResponse? GetRecording(string recordingId)
    {
        return _recordingsById.TryGetValue(recordingId, out var state) ? ToResponse(state) : null;
    }

    public string? GetRecordingFilePath(string recordingId)
    {
        return _recordingsById.TryGetValue(recordingId, out var state)
            ? state.FinalPath
            : null;
    }

    private void FinalizeRecording(RecordingState state)
    {
        if (state.FinalPath is not null)
        {
            return;
        }

        state.StoppedAtUtc = DateTime.UtcNow;
        var directory = Path.GetDirectoryName(state.TemporaryPath) ?? _environment.ContentRootPath;
        var finalName = $"{state.StartedAtUtc.ToLocalTime():HH-mm-ss.fff}_{state.StoppedAtUtc.Value.ToLocalTime():HH-mm-ss.fff}.mp4";
        var finalPath = Path.Combine(directory, finalName);

        try
        {
            if (File.Exists(state.TemporaryPath))
            {
                File.Move(state.TemporaryPath, finalPath, overwrite: true);
            }

            state.FinalPath = finalPath;
            _logger.LogInformation("Stopped recording {RecordingId}; saved to {Path}", state.RecordingId, finalPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to finalize recording {RecordingId}", state.RecordingId);
        }
        finally
        {
            _activeByCamera.TryRemove(state.CameraKey, out _);
        }
    }

    private string BuildCameraDirectory(CameraSource camera, DateTime startedAtUtc)
    {
        return Path.Combine(
            _environment.ContentRootPath,
            _recordingOptions.OutputRootPath,
            startedAtUtc.ToLocalTime().ToString("yyyy-MM-dd"),
            SanitizeSegment(camera.Name));
    }

    private static CameraRecordingResponse ToResponse(RecordingState state)
    {
        return new CameraRecordingResponse
        {
            RecordingId = state.RecordingId,
            CameraKey = state.CameraKey,
            CameraName = state.CameraName,
            StartedAtUtc = state.StartedAtUtc,
            StoppedAtUtc = state.StoppedAtUtc,
            IsRunning = state.FinalPath is null && !state.Process.HasExited,
            FileName = state.FinalPath is null ? null : Path.GetFileName(state.FinalPath),
        };
    }

    private static CameraStreamQuality ResolveQuality(string value)
    {
        return string.Equals(value, "Low", StringComparison.OrdinalIgnoreCase)
            ? CameraStreamQuality.Low
            : CameraStreamQuality.High;
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private static bool IsFfmpegError(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("error") || lower.Contains("failed") || lower.Contains("401") || lower.Contains("403");
    }

    private static string SanitizeFfmpegOutput(string value)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"rtsp://([^:\s/]+):([^@\s/]+)@",
            "rtsp://***:***@",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string SanitizeSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "camera" : sanitized;
    }
}
