using Microsoft.Extensions.Options;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class MotionMonitoringBackgroundService : BackgroundService
{
    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly MotionDetectionService _motionDetectionService;
    private readonly MotionFrameArchiveService _archiveService;
    private readonly MotionMonitoringOptions _options;
    private readonly ILogger<MotionMonitoringBackgroundService> _logger;

    public MotionMonitoringBackgroundService(
        ServerRegistryService registryService,
        RemoteFrameProxyService frameProxyService,
        MotionDetectionService motionDetectionService,
        MotionFrameArchiveService archiveService,
        IOptions<MotionMonitoringOptions> options,
        ILogger<MotionMonitoringBackgroundService> logger)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _motionDetectionService = motionDetectionService;
        _archiveService = archiveService;
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

                    if (_motionDetectionService.HasMotion(camera, frameBytes, _options, out var delta))
                    {
                        await _archiveService.SaveVideoFragmentAsync(
                            camera,
                            frameBytes,
                            token => _frameProxyService.GetFrameAsync(camera, token),
                            stoppingToken);
                        _logger.LogInformation(
                            "Motion detected for camera {CameraName} with delta {Delta:F2}",
                            camera.CameraName,
                            delta);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Motion polling failed for camera {CameraName}", camera.CameraName);
                }
            }

            await Task.Delay(_options.PollIntervalMilliseconds, stoppingToken);
        }
    }
}
