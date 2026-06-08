using System.Collections.Concurrent;
using CentralServer.Models;
using CentralisationService.Entities.Models.Vision;
using CentralisationService.Entities.Models.Zones;
using Microsoft.Extensions.Options;

namespace CentralServer.Services;

public sealed class RetailDetectionMonitoringBackgroundService : BackgroundService
{
    private readonly ServerRegistryService _registryService;
    private readonly RemoteFrameProxyService _frameProxyService;
    private readonly ZoneCatalogService _zoneCatalogService;
    private readonly RetailDetectionProfileCatalogService _profileCatalogService;
    private readonly NeuroRetailAnalysisService _neuroRetailAnalysisService;
    private readonly RetailDetectionEvidenceArchiveService _evidenceArchiveService;
    private readonly RetailDetectionMonitoringOptions _options;
    private readonly ILogger<RetailDetectionMonitoringBackgroundService> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _lastProfileExecutionUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lastEvidenceSaveUtc = new(StringComparer.OrdinalIgnoreCase);

    public RetailDetectionMonitoringBackgroundService(
        ServerRegistryService registryService,
        RemoteFrameProxyService frameProxyService,
        ZoneCatalogService zoneCatalogService,
        RetailDetectionProfileCatalogService profileCatalogService,
        NeuroRetailAnalysisService neuroRetailAnalysisService,
        RetailDetectionEvidenceArchiveService evidenceArchiveService,
        IOptions<RetailDetectionMonitoringOptions> options,
        ILogger<RetailDetectionMonitoringBackgroundService> logger)
    {
        _registryService = registryService;
        _frameProxyService = frameProxyService;
        _zoneCatalogService = zoneCatalogService;
        _profileCatalogService = profileCatalogService;
        _neuroRetailAnalysisService = neuroRetailAnalysisService;
        _evidenceArchiveService = evidenceArchiveService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retail detection iteration failed.");
            }

            await Task.Delay(Math.Max(1000, _options.LoopDelayMilliseconds), stoppingToken);
        }
    }

    private async Task ProcessIterationAsync(CancellationToken cancellationToken)
    {
        await _registryService.EnsureSynchronizedAsync(cancellationToken);
        var profiles = await _profileCatalogService.GetProfilesAsync(cancellationToken);
        if (profiles.Count == 0)
        {
            return;
        }

        var dueProfiles = profiles
            .Where(profile => profile.IsEnabled)
            .Where(profile => IsProfileDue(profile, DateTime.UtcNow))
            .GroupBy(profile => profile.CameraKey, StringComparer.OrdinalIgnoreCase);

        foreach (var cameraProfiles in dueProfiles)
        {
            var camera = _registryService.GetCamera(cameraProfiles.Key);
            if (camera is null || !camera.IsAvailable || string.IsNullOrWhiteSpace(camera.ServerBaseUrl))
            {
                continue;
            }

            try
            {
                var zones = await _zoneCatalogService.GetZonesAsync(camera.CameraKey, cancellationToken);
                if (zones.Count == 0)
                {
                    continue;
                }

                var relevantProfiles = cameraProfiles
                    .Where(profile => HasMatchingZones(profile, zones))
                    .ToArray();

                if (relevantProfiles.Length == 0)
                {
                    continue;
                }

                var frameBytes = await _frameProxyService.GetFrameAsync(camera, cancellationToken);
                var analysisRequest = BuildAnalysisRequest(camera, zones);
                analysisRequest = new RetailSceneAnalysisRequest
                {
                    SiteKey = analysisRequest.SiteKey,
                    CameraKey = analysisRequest.CameraKey,
                    Zones = analysisRequest.Zones,
                    FrameJpegBytes = frameBytes,
                };

                var analysisResponse = await _neuroRetailAnalysisService.AnalyzeAsync(analysisRequest, cancellationToken);
                var executedAtUtc = DateTime.UtcNow;

                foreach (var profile in relevantProfiles)
                {
                    _lastProfileExecutionUtc[profile.ProfileKey] = executedAtUtc;

                    var positive = IsPositive(profile, analysisResponse);
                    if (!positive || !profile.SaveEvidenceOnPositiveResult || !IsCooldownElapsed(profile, executedAtUtc))
                    {
                        continue;
                    }

                    var relativePath = await _evidenceArchiveService.SaveAsync(
                        camera,
                        BuildEvidenceReasonKey(profile),
                        frameBytes,
                        new SavedDetectionEvidenceMetadata
                        {
                            CompanyKey = camera.CompanyKey,
                            SiteKey = camera.SiteKey,
                            SiteName = camera.SiteName,
                            CameraKey = camera.CameraKey,
                            CameraName = camera.CameraName,
                            ProfileKey = profile.ProfileKey,
                            DetectionTypeKey = profile.DetectionTypeKey,
                            CapturedAtUtc = executedAtUtc,
                            ClientZoneHasPeople = analysisResponse.ClientZoneHasPeople,
                            IsSimulated = analysisResponse.IsSimulated,
                            Note = analysisResponse.Note,
                            Objects = BuildEvidenceObjects(profile, analysisResponse),
                        },
                        cancellationToken);

                    _lastEvidenceSaveUtc[profile.ProfileKey] = executedAtUtc;
                    _logger.LogInformation(
                        "Retail detection evidence saved for {ProfileKey} on camera {CameraKey}: {RelativePath}",
                        profile.ProfileKey,
                        camera.CameraKey,
                        relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retail detection processing failed for camera {CameraKey}", camera.CameraKey);
            }
        }
    }

    private static RetailSceneAnalysisRequest BuildAnalysisRequest(
        RemoteCameraState camera,
        IReadOnlyList<ZoneRecord> zones)
    {
        return new RetailSceneAnalysisRequest
        {
            SiteKey = camera.SiteKey,
            CameraKey = camera.CameraKey,
            Zones = zones.Select(zone => new RetailAnalysisZoneDto
            {
                ZoneTypeKey = zone.ZoneTypeKey,
                ZoneName = zone.ZoneName,
                DisplayName = zone.DisplayName,
                Points = zone.Points.Select(point => new ZonePointDto
                {
                    X = point.X,
                    Y = point.Y,
                }).ToArray(),
                Bounds = new ZoneBoundsDto
                {
                    X = zone.Bounds.X,
                    Y = zone.Bounds.Y,
                    Width = zone.Bounds.Width,
                    Height = zone.Bounds.Height,
                },
            }).ToArray(),
        };
    }

    private bool IsProfileDue(ConfiguredRetailDetectionProfile profile, DateTime utcNow)
    {
        if (_lastProfileExecutionUtc.TryGetValue(profile.ProfileKey, out var lastExecutionUtc))
        {
            return utcNow - lastExecutionUtc >= TimeSpan.FromSeconds(Math.Max(1, profile.IntervalSeconds));
        }

        return true;
    }

    private static bool HasMatchingZones(
        ConfiguredRetailDetectionProfile profile,
        IReadOnlyList<ZoneRecord> zones)
    {
        var hasClientZone = zones.Any(zone => string.Equals(zone.ZoneTypeKey, profile.ClientZoneTypeKey, StringComparison.OrdinalIgnoreCase));
        if (!hasClientZone)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.TargetZoneTypeKey))
        {
            return true;
        }

        return zones.Any(zone => string.Equals(zone.ZoneTypeKey, profile.TargetZoneTypeKey, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCooldownElapsed(ConfiguredRetailDetectionProfile profile, DateTime utcNow)
    {
        if (_lastEvidenceSaveUtc.TryGetValue(profile.ProfileKey, out var lastSaveUtc))
        {
            return utcNow - lastSaveUtc >= TimeSpan.FromSeconds(Math.Max(1, profile.CooldownSeconds));
        }

        return true;
    }

    private static bool IsPositive(
        ConfiguredRetailDetectionProfile profile,
        RetailSceneAnalysisResponse response)
    {
        if (string.Equals(profile.DetectionTypeKey, "client-presence-test", StringComparison.OrdinalIgnoreCase))
        {
            return response.ClientZoneHasPeople;
        }

        if (profile.RequiresClientZonePresence && !response.ClientZoneHasPeople)
        {
            return false;
        }

        var detection = response.Detections.FirstOrDefault(
            item => string.Equals(item.DetectionTypeKey, profile.DetectionTypeKey, StringComparison.OrdinalIgnoreCase));

        return detection?.IsDetected == true
            && (detection.Confidence ?? 0) >= Math.Clamp(profile.ConfidenceThreshold, 0.001, 0.999);
    }

    private static string BuildEvidenceReasonKey(ConfiguredRetailDetectionProfile profile)
    {
        return string.Equals(profile.DetectionTypeKey, "client-presence-test", StringComparison.OrdinalIgnoreCase)
            ? "client-presence-test"
            : $"{profile.DetectionTypeKey}-test";
    }

    private static IReadOnlyList<SavedDetectionObjectMetadata> BuildEvidenceObjects(
        ConfiguredRetailDetectionProfile profile,
        RetailSceneAnalysisResponse response)
    {
        var detection = response.Detections.FirstOrDefault(
            item => string.Equals(item.DetectionTypeKey, profile.DetectionTypeKey, StringComparison.OrdinalIgnoreCase));

        if (detection is null || detection.BoundingBoxes.Count == 0)
        {
            return Array.Empty<SavedDetectionObjectMetadata>();
        }

        return detection.BoundingBoxes
            .Select(bounds => new SavedDetectionObjectMetadata
            {
                DetectionTypeKey = detection.DetectionTypeKey,
                Label = detection.EvidenceLabel,
                Confidence = detection.Confidence,
                Bounds = new SavedDetectionBoundsMetadata
                {
                    X = bounds.X,
                    Y = bounds.Y,
                    Width = bounds.Width,
                    Height = bounds.Height,
                },
            })
            .ToArray();
    }
}
