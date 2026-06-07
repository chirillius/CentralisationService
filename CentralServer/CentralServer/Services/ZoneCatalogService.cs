using CentralServer.Models;
using CentralisationService.Entities.Models.Zones;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CentralServer.Services;

public sealed class ZoneCatalogService
{
    private readonly ZoneCatalogOptions _options;
    private readonly NpgsqlDataSource _dataSource;

    public ZoneCatalogService(IOptions<ZoneCatalogOptions> options, NpgsqlDataSource dataSource)
    {
        _options = options.Value;
        _dataSource = dataSource;
    }

    public async Task<ZoneNameCatalogDto> GetZoneNameCatalogAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT name
            FROM catalog.zone_name_templates
            WHERE is_enabled
            ORDER BY display_order, name;
            """;

        var names = new List<string>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return new ZoneNameCatalogDto
        {
            Names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            AllowCustom = true,
            CustomOptionLabel = _options.CustomOptionLabel,
        };
    }

    public async Task<IReadOnlyList<ZoneRecord>> GetZonesAsync(string cameraKey, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                zone.id,
                site.key AS site_key,
                camera.global_camera_key,
                zone.zone_type_key,
                zone.zone_name,
                zone.custom_name,
                zone.display_name,
                zone.bounds_x,
                zone.bounds_y,
                zone.bounds_width,
                zone.bounds_height,
                zone.created_at_utc,
                zone.updated_at_utc
            FROM catalog.zones zone
            JOIN catalog.sites site ON site.id = zone.site_id
            JOIN catalog.cameras camera ON camera.id = zone.camera_id
            WHERE camera.global_camera_key = @camera_key AND zone.is_enabled
            ORDER BY zone.display_name;
            """;

        var zones = new List<ZoneRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("camera_key", cameraKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            zones.Add(await ReadZoneAsync(reader, cancellationToken));
        }

        return zones;
    }

    public async Task<ZoneRecord?> GetZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                zone.id,
                site.key AS site_key,
                camera.global_camera_key,
                zone.zone_type_key,
                zone.zone_name,
                zone.custom_name,
                zone.display_name,
                zone.bounds_x,
                zone.bounds_y,
                zone.bounds_width,
                zone.bounds_height,
                zone.created_at_utc,
                zone.updated_at_utc
            FROM catalog.zones zone
            JOIN catalog.sites site ON site.id = zone.site_id
            JOIN catalog.cameras camera ON camera.id = zone.camera_id
            WHERE zone.id = @id AND zone.is_enabled;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", zoneId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? await ReadZoneAsync(reader, cancellationToken)
            : null;
    }

    public async Task<ZoneRecord> UpsertZoneAsync(UpsertZoneRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var displayName = ResolveDisplayName(request);
        var bounds = BuildBounds(request.Points);
        var now = DateTime.UtcNow;
        var zoneId = request.Id ?? Guid.NewGuid();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var scope = await ResolveZoneScopeAsync(connection, transaction, request.SiteKey, request.CameraKey, cancellationToken);
        const string zoneSql = """
            INSERT INTO catalog.zones (
                id,
                company_id,
                site_id,
                camera_id,
                zone_type_key,
                zone_name,
                custom_name,
                display_name,
                bounds_x,
                bounds_y,
                bounds_width,
                bounds_height,
                is_enabled,
                created_at_utc,
                updated_at_utc
            )
            VALUES (
                @id,
                @company_id,
                @site_id,
                @camera_id,
                @zone_type_key,
                @zone_name,
                @custom_name,
                @display_name,
                @bounds_x,
                @bounds_y,
                @bounds_width,
                @bounds_height,
                true,
                @now,
                @now
            )
            ON CONFLICT (id) DO UPDATE SET
                zone_type_key = EXCLUDED.zone_type_key,
                zone_name = EXCLUDED.zone_name,
                custom_name = EXCLUDED.custom_name,
                display_name = EXCLUDED.display_name,
                bounds_x = EXCLUDED.bounds_x,
                bounds_y = EXCLUDED.bounds_y,
                bounds_width = EXCLUDED.bounds_width,
                bounds_height = EXCLUDED.bounds_height,
                is_enabled = true,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """;

        await using (var command = new NpgsqlCommand(zoneSql, connection, transaction))
        {
            command.Parameters.AddWithValue("id", zoneId);
            command.Parameters.AddWithValue("company_id", scope.CompanyId);
            command.Parameters.AddWithValue("site_id", scope.SiteId);
            command.Parameters.AddWithValue("camera_id", scope.CameraId);
            command.Parameters.AddWithValue("zone_type_key", request.ZoneTypeKey.Trim());
            command.Parameters.AddWithValue("zone_name", request.ZoneName.Trim());
            command.Parameters.AddWithValue("custom_name", string.IsNullOrWhiteSpace(request.CustomName) ? DBNull.Value : request.CustomName.Trim());
            command.Parameters.AddWithValue("display_name", displayName);
            command.Parameters.AddWithValue("bounds_x", bounds.X);
            command.Parameters.AddWithValue("bounds_y", bounds.Y);
            command.Parameters.AddWithValue("bounds_width", bounds.Width);
            command.Parameters.AddWithValue("bounds_height", bounds.Height);
            command.Parameters.AddWithValue("now", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand("DELETE FROM catalog.zone_points WHERE zone_id = @zone_id;", connection, transaction))
        {
            command.Parameters.AddWithValue("zone_id", zoneId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string pointSql = """
            INSERT INTO catalog.zone_points (zone_id, point_index, x, y)
            VALUES (@zone_id, @point_index, @x, @y);
            """;
        for (var index = 0; index < request.Points.Count; index++)
        {
            await using var command = new NpgsqlCommand(pointSql, connection, transaction);
            command.Parameters.AddWithValue("zone_id", zoneId);
            command.Parameters.AddWithValue("point_index", index);
            command.Parameters.AddWithValue("x", request.Points[index].X);
            command.Parameters.AddWithValue("y", request.Points[index].Y);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ZoneRecord
        {
            Id = zoneId,
            SiteKey = request.SiteKey.Trim(),
            CameraKey = request.CameraKey.Trim(),
            ZoneTypeKey = request.ZoneTypeKey.Trim(),
            ZoneName = request.ZoneName.Trim(),
            CustomName = string.IsNullOrWhiteSpace(request.CustomName) ? null : request.CustomName.Trim(),
            DisplayName = displayName,
            Points = request.Points.Select(point => new ZonePointDto { X = point.X, Y = point.Y }).ToArray(),
            Bounds = bounds,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public async Task<bool> DeleteZoneAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE catalog.zones
            SET is_enabled = false,
                updated_at_utc = @now
            WHERE id = @id AND is_enabled;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", zoneId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<ZoneRecord> ReadZoneAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var zoneId = reader.GetGuid(0);
        return new ZoneRecord
        {
            Id = zoneId,
            SiteKey = reader.GetString(1),
            CameraKey = reader.GetString(2),
            ZoneTypeKey = reader.GetString(3),
            ZoneName = reader.GetString(4),
            CustomName = reader.IsDBNull(5) ? null : reader.GetString(5),
            DisplayName = reader.GetString(6),
            Bounds = new ZoneBoundsDto
            {
                X = reader.GetDouble(7),
                Y = reader.GetDouble(8),
                Width = reader.GetDouble(9),
                Height = reader.GetDouble(10),
            },
            Points = await GetZonePointsAsync(zoneId, cancellationToken),
            CreatedAtUtc = reader.GetDateTime(11),
            UpdatedAtUtc = reader.GetDateTime(12),
        };
    }

    private async Task<IReadOnlyList<ZonePointDto>> GetZonePointsAsync(Guid zoneId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT x, y
            FROM catalog.zone_points
            WHERE zone_id = @zone_id
            ORDER BY point_index;
            """;
        var points = new List<ZonePointDto>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("zone_id", zoneId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            points.Add(new ZonePointDto
            {
                X = reader.GetDouble(0),
                Y = reader.GetDouble(1),
            });
        }

        return points;
    }

    private static async Task<ZoneScope> ResolveZoneScopeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string siteKey,
        string cameraKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT company.id, site.id, camera.id
            FROM catalog.cameras camera
            JOIN catalog.sites site ON site.id = camera.site_id
            JOIN platform.companies company ON company.id = camera.company_id
            WHERE site.key = @site_key AND camera.global_camera_key = @camera_key AND camera.is_enabled
            LIMIT 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("site_key", siteKey.Trim());
        command.Parameters.AddWithValue("camera_key", cameraKey.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Камера не найдена в каталоге БД. Сначала синхронизируйте точку с Server.");
        }

        return new ZoneScope(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
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
            throw new ArgumentException("Нужно указать технический ключ точки.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CameraKey))
        {
            throw new ArgumentException("Нужно указать технический ключ камеры.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ZoneTypeKey))
        {
            throw new ArgumentException("Нужно указать тип зоны.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ZoneName))
        {
            throw new ArgumentException("Нужно указать название зоны.", nameof(request));
        }

        if (request.Points.Count < 3)
        {
            throw new ArgumentException("Полигон зоны должен содержать минимум три точки.", nameof(request));
        }

        if (request.Points.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1))
        {
            throw new ArgumentException("Координаты полигона должны быть нормализованы в диапазоне 0..1.", nameof(request));
        }
    }

    private sealed record ZoneScope(Guid CompanyId, Guid SiteId, Guid CameraId);
}
