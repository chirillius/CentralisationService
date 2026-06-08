using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class MotionMonitoringBackgroundService : BackgroundService
{
    private sealed class ActiveMotionRecording
    {
        public required RemoteCameraState Camera { get; init; }
        public required string RecordingId { get; init; }
        public required DateTime StartedAtUtc { get; init; }
        public required DateTime LastMotionAtUtc { get; set; }
        public bool IsStopping { get; set; }
    }

    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly MotionDetectionService _motionDetectionService;
    private readonly MotionFrameArchiveService _archiveService;
    private readonly RemoteRecordingService _remoteRecordingService;
    private readonly MotionMonitoringOptions _options;
    private readonly ILogger<MotionMonitoringBackgroundService> _logger;
    private readonly ConcurrentDictionary<string, ActiveMotionRecording> _activeRecordings = new(StringComparer.OrdinalIgnoreCase);

    public MotionMonitoringBackgroundService(
        ServerRegistryService registryService,
        RemoteFrameProxyService frameProxyService,
        MotionDetectionService motionDetectionService,
        MotionFrameArchiveService archiveService,
        RemoteRecordingService remoteRecordingService,
        IOptions<MotionMonitoringOptions> options,
        ILogger<MotionMonitoringBackgroundService> logger)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _motionDetectionService = motionDetectionService;
        _archiveService = archiveService;
        _remoteRecordingService = remoteRecordingService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _registryService.EnsureSynchronizedAsync(stoppingToken);
            var cameras = _registryService.GetAllCameras();

            foreach (var camera in cameras)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(camera.ServerBaseUrl) || !camera.IsAvailable)
                    {
                        continue;
                    }

                    var frameBytes = await _frameProxyService.GetFrameAsync(camera, stoppingToken);
                    var now = DateTime.UtcNow;
                    var hasMotion = _motionDetectionService.HasMotion(camera, frameBytes, _options, out var delta);

                    _logger.LogDebug(
                        "Motion check for camera {CameraName}: delta {Delta:F2}, threshold {Threshold:F2}, recording {IsRecording}",
                        camera.CameraName,
                        delta,
                        _options.MotionThreshold,
                        _activeRecordings.ContainsKey(camera.CameraKey));

                    if (hasMotion)
                    {
                        await EnsureRecordingStartedAsync(camera, now, stoppingToken);
                        _logger.LogInformation(
                            "Motion detected for camera {CameraName} with delta {Delta:F2}",
                            camera.CameraName,
                            delta);
                    }
                    else
                    {
                        await StopRecordingIfNeededAsync(camera.CameraKey, now, stoppingToken);
                    }

                    await StopRecordingIfMaxDurationReachedAsync(camera.CameraKey, now, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Motion polling failed for camera {CameraName}", camera.CameraName);
                }
            }

            await Task.Delay(_options.PollIntervalMilliseconds, stoppingToken);
        }
    }

    private async Task EnsureRecordingStartedAsync(RemoteCameraState camera, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (_activeRecordings.TryGetValue(camera.CameraKey, out var active))
        {
            active.LastMotionAtUtc = nowUtc;
            return;
        }

        var recording = await _remoteRecordingService.StartAsync(camera, _options, cancellationToken);
        _activeRecordings[camera.CameraKey] = new ActiveMotionRecording
        {
            Camera = camera,
            RecordingId = recording.RecordingId,
            StartedAtUtc = recording.StartedAtUtc == default ? nowUtc : recording.StartedAtUtc,
            LastMotionAtUtc = nowUtc,
        };
        _logger.LogInformation("Started remote motion recording {RecordingId} for camera {CameraName}", recording.RecordingId, camera.CameraName);
    }

    private async Task StopRecordingIfNeededAsync(string cameraKey, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!_activeRecordings.TryGetValue(cameraKey, out var active))
        {
            return;
        }

        var noMotionFor = nowUtc - active.LastMotionAtUtc;
        if (noMotionFor < TimeSpan.FromSeconds(Math.Max(1, _options.StopAfterNoMotionSeconds)))
        {
            return;
        }

        await StopAndDownloadAsync(cameraKey, active, cancellationToken);
    }

    private async Task StopRecordingIfMaxDurationReachedAsync(string cameraKey, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!_activeRecordings.TryGetValue(cameraKey, out var active))
        {
            return;
        }

        var maxDuration = TimeSpan.FromMinutes(Math.Max(1, _options.MaxRecordingMinutes));
        if (nowUtc - active.StartedAtUtc < maxDuration)
        {
            return;
        }

        await StopAndDownloadAsync(cameraKey, active, cancellationToken);
    }

    private async Task StopAndDownloadAsync(string cameraKey, ActiveMotionRecording active, CancellationToken cancellationToken)
    {
        if (active.IsStopping)
        {
            return;
        }

        active.IsStopping = true;
        try
        {
            var stopped = await _remoteRecordingService.StopAsync(active.Camera, active.RecordingId, cancellationToken)
                ?? new RemoteRecordingDto
                {
                    RecordingId = active.RecordingId,
                    CameraKey = active.Camera.SourceCameraKey,
                    CameraName = active.Camera.CameraName,
                    StartedAtUtc = active.StartedAtUtc,
                    StoppedAtUtc = DateTime.UtcNow,
                };
            var bytes = await _remoteRecordingService.DownloadAsync(active.Camera, active.RecordingId, cancellationToken);
            await _archiveService.SaveRemoteRecordingAsync(active.Camera, stopped, bytes, cancellationToken);
            _activeRecordings.TryRemove(cameraKey, out _);
            _logger.LogInformation("Downloaded remote motion recording {RecordingId} for camera {CameraName}", active.RecordingId, active.Camera.CameraName);
        }
        catch (Exception ex)
        {
            active.IsStopping = false;
            active.LastMotionAtUtc = DateTime.UtcNow;
            _logger.LogWarning(
                ex,
                "Failed to stop or download remote motion recording {RecordingId} for camera {CameraName}",
                active.RecordingId,
                active.Camera.CameraName);
        }
    }
}
