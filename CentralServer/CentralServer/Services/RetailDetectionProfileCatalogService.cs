using System.Text.Json;
using CentralServer.Models;
using Microsoft.Extensions.Options;

namespace CentralServer.Services;

public sealed class RetailDetectionProfileCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly RetailDetectionMonitoringOptions _options;
    private readonly IWebHostEnvironment _environment;

    public RetailDetectionProfileCatalogService(
        IOptions<RetailDetectionMonitoringOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public async Task<IReadOnlyList<ConfiguredRetailDetectionProfile>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        var path = EnsureConfigurationPath(_options.ProfilesFileName);
        if (!File.Exists(path))
        {
            return Array.Empty<ConfiguredRetailDetectionProfile>();
        }

        await using var stream = File.OpenRead(path);
        var profiles = await JsonSerializer.DeserializeAsync<List<ConfiguredRetailDetectionProfile>>(stream, SerializerOptions, cancellationToken);
        return profiles?
            .Where(profile => !string.IsNullOrWhiteSpace(profile.ProfileKey))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.CameraKey))
            .Where(profile => !string.IsNullOrWhiteSpace(profile.DetectionTypeKey))
            .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<ConfiguredRetailDetectionProfile>();
    }

    public async Task<ConfiguredRetailDetectionProfile> UpsertProfileAsync(
        ConfiguredRetailDetectionProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileKey))
        {
            throw new InvalidOperationException("profileKey is required.");
        }

        var path = EnsureConfigurationPath(_options.ProfilesFileName);
        var profiles = (await GetProfilesAsync(cancellationToken)).ToList();
        var existingIndex = profiles.FindIndex(
            item => string.Equals(item.ProfileKey, profile.ProfileKey, StringComparison.OrdinalIgnoreCase));

        var normalized = new ConfiguredRetailDetectionProfile
        {
            Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
            ProfileKey = profile.ProfileKey.Trim(),
            Name = profile.Name.Trim(),
            CameraKey = profile.CameraKey.Trim(),
            DetectionTypeKey = profile.DetectionTypeKey.Trim(),
            IsEnabled = profile.IsEnabled,
            ClientZoneTypeKey = string.IsNullOrWhiteSpace(profile.ClientZoneTypeKey) ? "client-zone" : profile.ClientZoneTypeKey.Trim(),
            TargetZoneTypeKey = string.IsNullOrWhiteSpace(profile.TargetZoneTypeKey) ? null : profile.TargetZoneTypeKey.Trim(),
            RequiresClientZonePresence = profile.RequiresClientZonePresence,
            SaveEvidenceOnPositiveResult = profile.SaveEvidenceOnPositiveResult,
            IntervalSeconds = Math.Max(1, profile.IntervalSeconds),
            CooldownSeconds = Math.Max(1, profile.CooldownSeconds),
            ConfidenceThreshold = Math.Clamp(profile.ConfidenceThreshold, 0.001, 0.999),
        };

        if (existingIndex >= 0)
        {
            profiles[existingIndex] = normalized;
        }
        else
        {
            profiles.Add(normalized);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profiles.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase), SerializerOptions, cancellationToken);
        return normalized;
    }

    private string EnsureConfigurationPath(string fileName)
    {
        var configurationRoot = Path.Combine(_environment.ContentRootPath, "Configuration");
        Directory.CreateDirectory(configurationRoot);
        return Path.Combine(configurationRoot, fileName);
    }
}
