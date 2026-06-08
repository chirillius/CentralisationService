using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using CentralServer.Models;
using CentralisationService.Entities.Models.Catalog;

namespace CentralServer.Services;

public sealed class ServerRegistryService
{
    private readonly ConcurrentDictionary<string, RegisteredServerState> _servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StoreCatalogOptions _options;
    private readonly AccessStoreService _accessStoreService;
    private readonly ILogger<ServerRegistryService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public ServerRegistryService(
        IHttpClientFactory httpClientFactory,
        IOptions<StoreCatalogOptions> options,
        AccessStoreService accessStoreService,
        ILogger<ServerRegistryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _accessStoreService = accessStoreService;
        _logger = logger;
    }

    public async Task EnsureSynchronizedAsync(CancellationToken cancellationToken)
    {
        if (_servers.IsEmpty || DateTime.UtcNow - _lastRefreshUtc >= TimeSpan.FromSeconds(Math.Max(5, _options.RefreshIntervalSeconds)))
        {
            await RefreshAsync(cancellationToken);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            foreach (var configuredStore in await GetConfiguredStoresAsync(cancellationToken))
            {
                await RefreshStoreAsync(configuredStore, cancellationToken);
            }

            _lastRefreshUtc = DateTime.UtcNow;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public IReadOnlyCollection<RegisteredServerState> GetServers(string? companyKey = null) =>
        _servers.Values
            .Where(server => string.IsNullOrWhiteSpace(companyKey) || string.Equals(server.CompanyKey, companyKey, StringComparison.OrdinalIgnoreCase))
            .Select(server =>
            {
                lock (server)
                {
                    var snapshot = new RegisteredServerState
                    {
                        CompanyKey = server.CompanyKey,
                        SiteKey = server.SiteKey,
                        SiteName = server.SiteName,
                        ServerBaseUrl = server.ServerBaseUrl,
                        CleaningDay = server.CleaningDay,
                        ConnectorId = server.ConnectorId,
                        ConnectorAccessToken = server.ConnectorAccessToken,
                        LastSyncUtc = server.LastSyncUtc,
                        IsAvailable = server.IsAvailable,
                    };

                    snapshot.Cameras.AddRange(server.Cameras.Select(camera => new RemoteCameraState
                    {
                        CompanyKey = camera.CompanyKey,
                        SiteKey = camera.SiteKey,
                        SiteName = camera.SiteName,
                        CameraKey = camera.CameraKey,
                        SourceCameraKey = camera.SourceCameraKey,
                        CameraId = camera.CameraId,
                        CameraName = camera.CameraName,
                        ServerBaseUrl = camera.ServerBaseUrl,
                        ConnectorAccessToken = camera.ConnectorAccessToken,
                        LastSyncUtc = camera.LastSyncUtc,
                        IsAvailable = camera.IsAvailable,
                    }));

                    return snapshot;
                }
            })
            .OrderBy(server => server.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyCollection<RemoteCameraState> GetAllCameras(string? siteKey = null, string? companyKey = null)
    {
        var cameras = _servers.Values
            .SelectMany(server =>
            {
                lock (server)
                {
                    return server.Cameras
                        .Select(camera => new RemoteCameraState
                        {
                            CompanyKey = camera.CompanyKey,
                            SiteKey = camera.SiteKey,
                            SiteName = camera.SiteName,
                            CameraKey = camera.CameraKey,
                            SourceCameraKey = camera.SourceCameraKey,
                            CameraId = camera.CameraId,
                            CameraName = camera.CameraName,
                            ServerBaseUrl = camera.ServerBaseUrl,
                            ConnectorAccessToken = camera.ConnectorAccessToken,
                            LastSyncUtc = camera.LastSyncUtc,
                            IsAvailable = camera.IsAvailable,
                        })
                        .ToArray();
                }
            });

        if (!string.IsNullOrWhiteSpace(siteKey))
        {
            cameras = cameras.Where(camera => string.Equals(camera.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(companyKey))
        {
            cameras = cameras.Where(camera => string.Equals(camera.CompanyKey, companyKey, StringComparison.OrdinalIgnoreCase));
        }

        return cameras
            .OrderBy(camera => camera.SiteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(camera => camera.CameraName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public RemoteCameraState? GetCamera(string cameraKey, string? companyKey = null) =>
        _servers.Values
            .SelectMany(server =>
            {
                lock (server)
                {
                    return server.Cameras
                        .Select(camera => new RemoteCameraState
                        {
                            CompanyKey = camera.CompanyKey,
                            SiteKey = camera.SiteKey,
                            SiteName = camera.SiteName,
                            CameraKey = camera.CameraKey,
                            SourceCameraKey = camera.SourceCameraKey,
                            CameraId = camera.CameraId,
                            CameraName = camera.CameraName,
                            ServerBaseUrl = camera.ServerBaseUrl,
                            ConnectorAccessToken = camera.ConnectorAccessToken,
                            LastSyncUtc = camera.LastSyncUtc,
                            IsAvailable = camera.IsAvailable,
                        })
                        .ToArray();
                }
            })
            .FirstOrDefault(camera =>
                string.Equals(camera.CameraKey, cameraKey, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(companyKey) || string.Equals(camera.CompanyKey, companyKey, StringComparison.OrdinalIgnoreCase)));

    private async Task RefreshStoreAsync(ConfiguredStoreOptions configuredStore, CancellationToken cancellationToken)
    {
        var company = await _accessStoreService.GetCompanyByKeyAsync(configuredStore.CompanyKey, cancellationToken);
        if (company is null || !await _accessStoreService.IsCompanyActiveAsync(company.Id, cancellationToken))
        {
            _servers.TryRemove(configuredStore.SiteKey, out _);
            return;
        }

        var normalizedBaseUrl = configuredStore.ServerBaseUrl.TrimEnd('/');
        var syncUtc = DateTime.UtcNow;
        var state = _servers.AddOrUpdate(
            configuredStore.SiteKey,
            _ => new RegisteredServerState
            {
                CompanyKey = configuredStore.CompanyKey,
                SiteKey = configuredStore.SiteKey,
                SiteName = configuredStore.SiteName,
                ServerBaseUrl = normalizedBaseUrl,
                CleaningDay = configuredStore.CleaningDay,
                ConnectorAccessToken = configuredStore.ConnectorAccessToken,
            },
            (_, existing) => existing);

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(ServerRegistryService));
            if (!string.IsNullOrWhiteSpace(configuredStore.ConnectorAccessToken))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Connector-Token", configuredStore.ConnectorAccessToken);
            }

            var connectorInfo = await client.GetFromJsonAsync<RemoteConnectorInfoDto>(
                $"{normalizedBaseUrl}/api/connector/info",
                cancellationToken);

            var cameras = await client.GetFromJsonAsync<List<RemoteServerCameraDto>>(
                $"{normalizedBaseUrl}/api/cameras",
                cancellationToken);

            if (connectorInfo is null)
            {
                throw new InvalidOperationException($"Server '{normalizedBaseUrl}' returned empty connector info.");
            }

            if (cameras is null)
            {
                throw new InvalidOperationException($"Server '{normalizedBaseUrl}' returned empty camera list.");
            }

            lock (state)
            {
                state.ConnectorId = string.IsNullOrWhiteSpace(connectorInfo.ConnectorId)
                    ? configuredStore.SiteKey
                    : connectorInfo.ConnectorId;
                state.ConnectorAccessToken = configuredStore.ConnectorAccessToken;
                state.SiteName = string.IsNullOrWhiteSpace(configuredStore.SiteName)
                    ? connectorInfo.SiteName
                    : configuredStore.SiteName;
                state.LastSyncUtc = syncUtc;
                state.IsAvailable = true;

                state.Cameras.Clear();
                foreach (var camera in cameras)
                {
                    var localCameraKey = string.IsNullOrWhiteSpace(camera.Key)
                        ? (camera.Id?.ToString() ?? camera.Name)
                        : camera.Key;

                    state.Cameras.Add(new RemoteCameraState
                    {
                        CompanyKey = state.CompanyKey,
                        SiteKey = state.SiteKey,
                        SiteName = state.SiteName,
                        CameraKey = BuildGlobalCameraKey(state.SiteKey, localCameraKey),
                        SourceCameraKey = localCameraKey,
                        CameraId = camera.Id,
                        CameraName = camera.Name,
                        Host = camera.Host,
                        HighQualityPath = camera.HighQualityPath,
                        LowQualityPath = camera.LowQualityPath,
                        ServerBaseUrl = normalizedBaseUrl,
                        ConnectorAccessToken = configuredStore.ConnectorAccessToken,
                        LastSyncUtc = syncUtc,
                        IsAvailable = true,
                    });
                }
            }

            await _accessStoreService.UpsertSyncedCamerasAsync(
                state.CompanyKey,
                state.SiteKey,
                normalizedBaseUrl,
                state.Cameras,
                cancellationToken);
        }
        catch (Exception ex)
        {
            lock (state)
            {
                state.IsAvailable = false;
                foreach (var camera in state.Cameras)
                {
                    camera.IsAvailable = false;
                }
            }

            _logger.LogWarning(ex, "Failed to refresh configured store {SiteKey} from {ServerBaseUrl}", configuredStore.SiteKey, normalizedBaseUrl);
        }
    }

    private static string BuildGlobalCameraKey(string siteKey, string sourceCameraKey) =>
        $"{siteKey}:{sourceCameraKey}";

    private async Task<List<ConfiguredStoreOptions>> GetConfiguredStoresAsync(CancellationToken cancellationToken)
    {
        var configuredStores = _options.Stores.ToList();
        var dynamicStores = (await _accessStoreService.GetCompanySitesAsync(cancellationToken))
            .Where(site => site.DisabledAtUtc is null)
            .Select(ToConfiguredStore);

        configuredStores.AddRange(dynamicStores);
        return configuredStores;
    }

    private static ConfiguredStoreOptions ToConfiguredStore(CompanySiteBinding binding) => new()
    {
        CompanyKey = binding.CompanyKey,
        SiteKey = binding.SiteKey,
        SiteName = binding.SiteName,
        ServerBaseUrl = binding.ServerBaseUrl,
        CleaningDay = binding.CleaningDay,
        ConnectorAccessToken = binding.ConnectorAccessToken,
    };
}
