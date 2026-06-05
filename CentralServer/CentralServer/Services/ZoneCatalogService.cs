using System.Text.Json;
using CentralServer.Models;
using CentralisationService.Entities.Models.Zones;
using Microsoft.Extensions.Options;

namespace CentralServer.Services;

public sealed class ZoneCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly ZoneCatalogOptions _options;
    private readonly ILogger<ZoneCatalogService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ZoneCatalogService(IOptions<ZoneCatalogOptions> options, ILogger<ZoneCatalogService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ZoneNameCatalogDto> GetZoneNameCatalogAsync(CancellationToken cancellationToken)
    {
        var zoneNamesPath = EnsureConfigurationPath(_options.ZoneNamesFileName);

        if (!File.Exists(zoneNamesPath))
        {
            await File.WriteAllTextAsync(zoneNamesPath, "[]", cancellationToken);
        }

        await using var stream = File.OpenRead(zoneNamesPath);
        var names = await JsonSerializer.DeserializeAsync<List<string>>(stream, JsonOptions, cancellationToken) ?? new List<string>();

        return new ZoneNameCatalogDto
        {
            Names = names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            AllowCustom = true,
            CustomOptionLabel = _options.CustomOptionLabel,
        };
    }

    public async Task<IReadOnlyList<ZoneRecord>> GetZonesAsync(string cameraKey, CancellationToken cancellationToken)
    {
        var zones = await LoadZonesAsync(cancellationToken);
        return zones
            .Where(zone => string.Equals(zone.CameraKey, cameraKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(zone => zone.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ZoneRecord?> GetZoneAsync(Guid zoneId, CancellationToken cancellationToken) =>
        (await LoadZonesAsync(cancellationToken)).FirstOrDefault(zone => zone.Id == zoneId);

    public async Task<ZoneRecord> UpsertZoneAsync(UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var zones = await LoadZonesAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var displayName = ResolveDisplayName(request);
            var bounds = BuildBounds(request.Points);
            var zoneId = request.Id ?? Guid.NewGuid();
            var existingIndex = zones.FindIndex(zone => zone.Id == zoneId);
            var createdAtUtc = existingIndex >= 0 ? zones[existingIndex].CreatedAtUtc : now;

            var record = new ZoneRecord
            {
                Id = zoneId,
                SiteKey = request.SiteKey.Trim(),
                CameraKey = request.CameraKey.Trim(),
                ZoneTypeKey = request.ZoneTypeKey.Trim(),
                ZoneName = request.ZoneName.Trim(),
                CustomName = string.IsNullOrWhiteSpace(request.CustomName) ? null : request.CustomName.Trim(),
                DisplayName = displayName,
                Points = request.Points
                    .Select(point => new ZonePointDto { X = point.X, Y = point.Y })
                    .ToArray(),
                Bounds = bounds,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = now,
            };

            if (existingIndex >= 0)
            {
                zones[existingIndex] = record;
            }
            else
            {
                zones.Add(record);
            }

            await SaveZonesAsync(zones, cancellationToken);
            return record;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var zones = await LoadZonesAsync(cancellationToken);
            var removed = zones.RemoveAll(zone => zone.Id == zoneId) > 0;
            if (removed)
            {
                await SaveZonesAsync(zones, cancellationToken);
            }

            return removed;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public string EnsureConfigurationPath(string fileName)
    {
        var configurationDirectory = Path.IsPathRooted(_options.ConfigurationDirectory)
            ? _options.ConfigurationDirectory
            : Path.Combine(Directory.GetCurrentDirectory(), _options.ConfigurationDirectory);

        Directory.CreateDirectory(configurationDirectory);
        var path = Path.Combine(configurationDirectory, fileName);
        _logger.LogDebug("Using zone catalog path {Path}", path);
        return path;
    }

    private async Task<List<ZoneRecord>> LoadZonesAsync(CancellationToken cancellationToken)
    {
        var zonesPath = EnsureConfigurationPath(_options.ZonesFileName);

        if (!File.Exists(zonesPath))
        {
            await File.WriteAllTextAsync(zonesPath, "[]", cancellationToken);
        }

        await using var stream = File.OpenRead(zonesPath);
        return await JsonSerializer.DeserializeAsync<List<ZoneRecord>>(stream, JsonOptions, cancellationToken) ?? new List<ZoneRecord>();
    }

    private async Task SaveZonesAsync(List<ZoneRecord> zones, CancellationToken cancellationToken)
    {
        var zonesPath = EnsureConfigurationPath(_options.ZonesFileName);
        await using var stream = File.Create(zonesPath);
        await JsonSerializer.SerializeAsync(stream, zones, JsonOptions, cancellationToken);
    }

    private static string ResolveDisplayName(UpsertZoneRequest request) =>
        string.IsNullOrWhiteSpace(request.CustomName)
            ? request.ZoneName.Trim()
            : request.CustomName.Trim();

    private static ZoneBoundsDto BuildBounds(IReadOnlyList<ZonePointDto> points)
    {
        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);

        return new ZoneBoundsDto
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
        };
    }

    private static void ValidateRequest(UpsertZoneRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SiteKey))
        {
            throw new ArgumentException("Site key is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CameraKey))
        {
            throw new ArgumentException("Camera key is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ZoneTypeKey))
        {
            throw new ArgumentException("Zone type key is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ZoneName))
        {
            throw new ArgumentException("Zone name is required.", nameof(request));
        }

        if (request.Points.Count < 3)
        {
            throw new ArgumentException("Polygon zone must contain at least three points.", nameof(request));
        }

        if (request.Points.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1))
        {
            throw new ArgumentException("Polygon coordinates must be normalized in the 0..1 range.", nameof(request));
        }
    }
}
