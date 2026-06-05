using System.Collections.Concurrent;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class MotionFrameIndexService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<MotionFrameRecord>> _recordsByCamera =
        new(StringComparer.OrdinalIgnoreCase);

    public void Add(MotionFrameRecord record)
    {
        var queue = _recordsByCamera.GetOrAdd(
            record.CameraKey,
            _ => new ConcurrentQueue<MotionFrameRecord>());

        queue.Enqueue(record);

        while (queue.Count > 100)
        {
            queue.TryDequeue(out _);
        }
    }

    public IReadOnlyList<MotionFrameRecord> GetRecent(string? cameraKey = null, int take = 30)
    {
        if (!string.IsNullOrWhiteSpace(cameraKey) &&
            _recordsByCamera.TryGetValue(cameraKey, out var queue))
        {
            return queue.Reverse().Take(take).ToArray();
        }

        return _recordsByCamera.Values
            .SelectMany(queue => queue)
            .OrderByDescending(record => record.CapturedAtUtc)
            .Take(take)
            .ToArray();
    }

    public MotionFrameRecord? FindByRelativePath(string relativePath)
    {
        return _recordsByCamera.Values
            .SelectMany(queue => queue)
            .FirstOrDefault(record =>
                string.Equals(record.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }
}
