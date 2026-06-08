using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class MotionDetectionService
{
    private readonly Dictionary<string, float[]> _previousFrames = new(StringComparer.OrdinalIgnoreCase);

    public bool HasMotion(
        RemoteCameraState camera,
        byte[] jpegBytes,
        MotionMonitoringOptions options,
        out double delta)
    {
        using var image = Image.Load<Rgba32>(jpegBytes);
        image.Mutate(ctx => ctx.Resize(options.ThumbnailWidth, options.ThumbnailHeight).Grayscale());

        var currentSignature = new float[options.ThumbnailWidth * options.ThumbnailHeight];
        var index = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    currentSignature[index++] = row[x].R;
                }
            }
        });

        if (!_previousFrames.TryGetValue(camera.CameraKey, out var previousSignature))
        {
            _previousFrames[camera.CameraKey] = currentSignature;
            delta = 0d;
            return false;
        }

        var sum = 0d;
        for (var i = 0; i < currentSignature.Length; i++)
        {
            sum += Math.Abs(currentSignature[i] - previousSignature[i]);
        }

        delta = sum / currentSignature.Length;
        _previousFrames[camera.CameraKey] = currentSignature;

        if (delta < options.MotionThreshold)
        {
            return false;
        }

        return true;
    }
}
