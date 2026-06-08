using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Server.Models;

namespace Server.Services;

public sealed class CameraConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IOptionsMonitor<ServerNodeOptions> _options;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public CameraConfigurationService(IWebHostEnvironment environment, IOptionsMonitor<ServerNodeOptions> options)
    {
        _environment = environment;
        _options = options;
    }

    public IReadOnlyList<CameraSource> GetCameras()
    {
        return _options.CurrentValue.Cameras;
    }

    public async Task<CameraSource> UpsertAsync(CameraConfigurationRequest request, string? existingKey, CancellationToken cancellationToken)
    {
        var cameras = GetCameras().Select(Clone).ToList();
        var normalized = Normalize(request, cameras, existingKey);
        var index = existingKey is null
            ? -1
            : cameras.FindIndex(camera => string.Equals(camera.ResolveKey(), existingKey, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            cameras[index] = normalized;
        }
        else
        {
            cameras.Add(normalized);
        }

        await SaveCamerasAsync(cameras, cancellationToken);
        return normalized;
    }

    public async Task<bool> DeleteAsync(string cameraKey, CancellationToken cancellationToken)
    {
        var cameras = GetCameras().Select(Clone).ToList();
        var removed = cameras.RemoveAll(camera => string.Equals(camera.ResolveKey(), cameraKey, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
        {
            return false;
        }

        await SaveCamerasAsync(cameras, cancellationToken);
        return true;
    }

    private async Task SaveCamerasAsync(IReadOnlyList<CameraSource> cameras, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var path = Path.Combine(_environment.ContentRootPath, "appsettings.json");
            var root = File.Exists(path)
                ? JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken))?.AsObject() ?? new JsonObject()
                : new JsonObject();

            var serverNode = root["ServerNode"]?.AsObject() ?? new JsonObject();
            root["ServerNode"] = serverNode;
            serverNode["Cameras"] = JsonSerializer.SerializeToNode(cameras, JsonOptions);

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(temporaryPath, root.ToJsonString(JsonOptions), cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static CameraSource Normalize(CameraConfigurationRequest request, IReadOnlyList<CameraSource> cameras, string? existingKey)
    {
        var host = request.Host.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Нужно указать IP-адрес или host камеры.");
        }

        var key = string.IsNullOrWhiteSpace(request.Key)
            ? BuildKeyFromName(request.Name)
            : request.Key.Trim();
        var id = request.Id ?? ResolveNextId(cameras, existingKey);

        return new CameraSource
        {
            Id = id,
            Key = key,
            Name = request.Name.Trim(),
            Host = host,
            HighQualityPath = NormalizePath(request.HighQualityPath, "/Streaming/Channels/101"),
            LowQualityPath = NormalizePath(request.LowQualityPath, "/Streaming/Channels/102"),
            Address = string.Empty,
            StreamAddress = null,
        };
    }

    private static CameraSource Clone(CameraSource camera)
    {
        return new CameraSource
        {
            Id = camera.Id,
            Key = camera.Key,
            Name = camera.Name,
            Address = camera.Address,
            StreamAddress = camera.StreamAddress,
            Host = camera.Host,
            HighQualityPath = camera.HighQualityPath,
            LowQualityPath = camera.LowQualityPath,
        };
    }

    private static int ResolveNextId(IReadOnlyList<CameraSource> cameras, string? existingKey)
    {
        if (existingKey is not null)
        {
            var existing = cameras.FirstOrDefault(camera => string.Equals(camera.ResolveKey(), existingKey, StringComparison.OrdinalIgnoreCase));
            if (existing?.Id is not null)
            {
                return existing.Id.Value;
            }
        }

        return cameras.Select(camera => camera.Id ?? -1).DefaultIfEmpty(-1).Max() + 1;
    }

    private static string NormalizePath(string? value, string fallback)
    {
        var path = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return path.StartsWith('/') ? path : $"/{path}";
    }

    private static string BuildKeyFromName(string name)
    {
        var source = new CameraSource { Name = name.Trim(), Address = string.Empty };
        return source.ResolveKey();
    }
}
