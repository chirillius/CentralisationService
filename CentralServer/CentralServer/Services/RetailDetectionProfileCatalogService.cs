using System.Text.Json;
using CentralServer.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CentralServer.Services;

public sealed class RetailDetectionProfileCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly RetailDetectionMonitoringOptions _options;
    private readonly PostgreSqlOptions _postgreSqlOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly NpgsqlDataSource _dataSource;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private bool _seeded;

    public RetailDetectionProfileCatalogService(
        IOptions<RetailDetectionMonitoringOptions> options,
        IOptions<PostgreSqlOptions> postgreSqlOptions,
        IWebHostEnvironment environment,
        NpgsqlDataSource dataSource)
    {
        _options = options.Value;
        _postgreSqlOptions = postgreSqlOptions.Value;
        _environment = environment;
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<ConfiguredRetailDetectionProfile>> GetProfilesAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        const string sql = """
            SELECT
                profile.id,
                profile.key,
                profile.name,
                COALESCE(camera.global_camera_key, profile.settings ->> 'cameraKey') AS camera_key,
                detection_type.key AS detection_type_key,
                profile.is_enabled,
                COALESCE(profile.client_zone_type_key, 'client-zone') AS client_zone_type_key,
                profile.target_zone_type_key,
                profile.requires_client_zone_presence,
                profile.save_evidence_on_positive_result,
                profile.interval_seconds,
                profile.cooldown_seconds,
                COALESCE(profile.confidence_threshold, 0.25) AS confidence_threshold
            FROM detection.detection_profiles profile
            JOIN detection.detection_types detection_type ON detection_type.id = profile.detection_type_id
            LEFT JOIN catalog.cameras camera ON camera.id = profile.camera_id
            ORDER BY profile.name;
            """;

        var profiles = new List<ConfiguredRetailDetectionProfile>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var cameraKey = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            if (string.IsNullOrWhiteSpace(cameraKey))
            {
                continue;
            }

            profiles.Add(new ConfiguredRetailDetectionProfile
            {
                Id = reader.GetGuid(0),
                ProfileKey = reader.GetString(1),
                Name = reader.GetString(2),
                CameraKey = cameraKey,
                DetectionTypeKey = reader.GetString(4),
                IsEnabled = reader.GetBoolean(5),
                ClientZoneTypeKey = reader.GetString(6),
                TargetZoneTypeKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                RequiresClientZonePresence = reader.GetBoolean(8),
                SaveEvidenceOnPositiveResult = reader.GetBoolean(9),
                IntervalSeconds = reader.GetInt32(10),
                CooldownSeconds = reader.GetInt32(11),
                ConfidenceThreshold = reader.GetDouble(12),
            });
        }

        return profiles;
    }

    public async Task<ConfiguredRetailDetectionProfile> UpsertProfileAsync(
        ConfiguredRetailDetectionProfile profile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileKey))
        {
            throw new InvalidOperationException("Нужно указать технический ключ профиля.");
        }

        await EnsureSeededAsync(cancellationToken);
        var normalized = NormalizeProfile(profile);
        var scope = await ResolveProfileScopeAsync(normalized.CameraKey, normalized.DetectionTypeKey, cancellationToken);
        var settingsJson = JsonSerializer.Serialize(new { cameraKey = normalized.CameraKey }, SerializerOptions);

        const string sql = """
            INSERT INTO detection.detection_profiles (
                id,
                company_id,
                site_id,
                camera_id,
                server_node_id,
                detection_type_id,
                key,
                name,
                is_enabled,
                interval_seconds,
                cooldown_seconds,
                confidence_threshold,
                requires_client_zone_presence,
                client_zone_type_key,
                target_zone_type_key,
                save_evidence_on_positive_result,
                settings,
                updated_at_utc
            )
            VALUES (
                @id,
                @company_id,
                @site_id,
                @camera_id,
                @server_node_id,
                @detection_type_id,
                @key,
                @name,
                @is_enabled,
                @interval_seconds,
                @cooldown_seconds,
                @confidence_threshold,
                @requires_client_zone_presence,
                @client_zone_type_key,
                @target_zone_type_key,
                @save_evidence_on_positive_result,
                CAST(@settings AS jsonb),
                @updated_at_utc
            )
            ON CONFLICT (company_id, key) DO UPDATE SET
                site_id = EXCLUDED.site_id,
                camera_id = EXCLUDED.camera_id,
                server_node_id = EXCLUDED.server_node_id,
                detection_type_id = EXCLUDED.detection_type_id,
                name = EXCLUDED.name,
                is_enabled = EXCLUDED.is_enabled,
                interval_seconds = EXCLUDED.interval_seconds,
                cooldown_seconds = EXCLUDED.cooldown_seconds,
                confidence_threshold = EXCLUDED.confidence_threshold,
                requires_client_zone_presence = EXCLUDED.requires_client_zone_presence,
                client_zone_type_key = EXCLUDED.client_zone_type_key,
                target_zone_type_key = EXCLUDED.target_zone_type_key,
                save_evidence_on_positive_result = EXCLUDED.save_evidence_on_positive_result,
                settings = EXCLUDED.settings,
                updated_at_utc = EXCLUDED.updated_at_utc
            RETURNING id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", normalized.Id == Guid.Empty ? Guid.NewGuid() : normalized.Id);
        command.Parameters.AddWithValue("company_id", scope.CompanyId);
        command.Parameters.AddWithValue("site_id", scope.SiteId);
        command.Parameters.AddWithValue("camera_id", (object?)scope.CameraId ?? DBNull.Value);
        command.Parameters.AddWithValue("server_node_id", (object?)scope.ServerNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("detection_type_id", scope.DetectionTypeId);
        command.Parameters.AddWithValue("key", normalized.ProfileKey);
        command.Parameters.AddWithValue("name", normalized.Name);
        command.Parameters.AddWithValue("is_enabled", normalized.IsEnabled);
        command.Parameters.AddWithValue("interval_seconds", normalized.IntervalSeconds);
        command.Parameters.AddWithValue("cooldown_seconds", normalized.CooldownSeconds);
        command.Parameters.AddWithValue("confidence_threshold", normalized.ConfidenceThreshold);
        command.Parameters.AddWithValue("requires_client_zone_presence", normalized.RequiresClientZonePresence);
        command.Parameters.AddWithValue("client_zone_type_key", normalized.ClientZoneTypeKey);
        command.Parameters.AddWithValue("target_zone_type_key", (object?)normalized.TargetZoneTypeKey ?? DBNull.Value);
        command.Parameters.AddWithValue("save_evidence_on_positive_result", normalized.SaveEvidenceOnPositiveResult);
        command.Parameters.AddWithValue("settings", settingsJson);
        command.Parameters.AddWithValue("updated_at_utc", DateTime.UtcNow);
        var id = (Guid)(await command.ExecuteScalarAsync(cancellationToken) ?? normalized.Id);

        return new ConfiguredRetailDetectionProfile
        {
            Id = id,
            ProfileKey = normalized.ProfileKey,
            Name = normalized.Name,
            CameraKey = normalized.CameraKey,
            DetectionTypeKey = normalized.DetectionTypeKey,
            IsEnabled = normalized.IsEnabled,
            ClientZoneTypeKey = normalized.ClientZoneTypeKey,
            TargetZoneTypeKey = normalized.TargetZoneTypeKey,
            RequiresClientZonePresence = normalized.RequiresClientZonePresence,
            SaveEvidenceOnPositiveResult = normalized.SaveEvidenceOnPositiveResult,
            IntervalSeconds = normalized.IntervalSeconds,
            CooldownSeconds = normalized.CooldownSeconds,
            ConfidenceThreshold = normalized.ConfidenceThreshold,
        };
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded)
        {
            return;
        }

        await _seedLock.WaitAsync(cancellationToken);
        try
        {
            if (_seeded || await HasProfilesAsync(cancellationToken) || !_postgreSqlOptions.SeedJsonConfigurationOnEmptyDatabase)
            {
                _seeded = true;
                return;
            }

            var path = EnsureConfigurationPath(_options.ProfilesFileName);
            if (!File.Exists(path))
            {
                _seeded = true;
                return;
            }

            await using var stream = File.OpenRead(path);
            var profiles = await JsonSerializer.DeserializeAsync<List<ConfiguredRetailDetectionProfile>>(stream, SerializerOptions, cancellationToken)
                ?? [];
            foreach (var profile in profiles)
            {
                try
                {
                    await UpsertProfileWithoutSeedAsync(profile, cancellationToken);
                }
                catch
                {
                    // Profiles can reference cameras that are not synchronized yet.
                    // Keep the service bootable; the profile can be saved again from UI after sync.
                }
            }

            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private async Task<ConfiguredRetailDetectionProfile> UpsertProfileWithoutSeedAsync(
        ConfiguredRetailDetectionProfile profile,
        CancellationToken cancellationToken)
    {
        _seeded = true;
        return await UpsertProfileAsync(profile, cancellationToken);
    }

    private async Task<bool> HasProfilesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM detection.detection_profiles);", connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<ProfileScope> ResolveProfileScopeAsync(string cameraKey, string detectionTypeKey, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                company.id,
                site.id,
                camera.id,
                camera.server_node_id,
                detection_type.id
            FROM catalog.sites site
            JOIN platform.companies company ON company.id = site.company_id
            LEFT JOIN catalog.cameras camera ON camera.site_id = site.id AND camera.global_camera_key = @camera_key
            JOIN detection.detection_types detection_type ON detection_type.key = @detection_type_key
            WHERE site.key = split_part(@camera_key, ':', 1)
            LIMIT 1;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("camera_key", cameraKey.Trim());
        command.Parameters.AddWithValue("detection_type_key", detectionTypeKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Не найдена точка или тип фиксации для профиля модели.");
        }

        return new ProfileScope(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetGuid(4));
    }

    private ConfiguredRetailDetectionProfile NormalizeProfile(ConfiguredRetailDetectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.CameraKey))
        {
            throw new InvalidOperationException("Нужно указать камеру профиля.");
        }
        if (string.IsNullOrWhiteSpace(profile.DetectionTypeKey))
        {
            throw new InvalidOperationException("Нужно указать тип фиксации профиля.");
        }

        return new ConfiguredRetailDetectionProfile
        {
            Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
            ProfileKey = profile.ProfileKey.Trim(),
            Name = string.IsNullOrWhiteSpace(profile.Name) ? profile.ProfileKey.Trim() : profile.Name.Trim(),
            CameraKey = profile.CameraKey.Trim(),
            DetectionTypeKey = profile.DetectionTypeKey.Trim(),
            IsEnabled = profile.IsEnabled,
            ClientZoneTypeKey = string.IsNullOrWhiteSpace(profile.ClientZoneTypeKey) ? "client-zone" : profile.ClientZoneTypeKey.Trim(),
            TargetZoneTypeKey = string.IsNullOrWhiteSpace(profile.TargetZoneTypeKey) ? null : profile.TargetZoneTypeKey.Trim(),
            RequiresClientZonePresence = profile.RequiresClientZonePresence,
            SaveEvidenceOnPositiveResult = profile.SaveEvidenceOnPositiveResult,
            IntervalSeconds = Math.Max(1, profile.IntervalSeconds),
            CooldownSeconds = Math.Max(0, profile.CooldownSeconds),
            ConfidenceThreshold = Math.Clamp(profile.ConfidenceThreshold, 0.001, 0.999),
        };
    }

    private string EnsureConfigurationPath(string fileName)
    {
        var configurationRoot = Path.Combine(_environment.ContentRootPath, "Configuration");
        Directory.CreateDirectory(configurationRoot);
        return Path.Combine(configurationRoot, fileName);
    }

    private sealed record ProfileScope(
        Guid CompanyId,
        Guid SiteId,
        Guid? CameraId,
        Guid? ServerNodeId,
        Guid DetectionTypeId);
}
