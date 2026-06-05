using CentralisationService.Entities.Models.Vision;
using CentralisationService.Entities.Models.Zones;
using Neuro.Models;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Neuro.Services;

public sealed class RetailAnalysisService : IDisposable
{
    private const int PersonClassId = 0;

    private readonly RetailModelOptions _options;
    private readonly ILogger<RetailAnalysisService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly Lazy<YoloOnnxDetector?> _personDetector;
    private readonly Lazy<YoloOnnxDetector?> _phoneDetector;
    private readonly Lazy<YoloOnnxDetector?> _bottlesDetector;

    public RetailAnalysisService(
        IOptions<RetailModelOptions> options,
        IWebHostEnvironment environment,
        ILogger<RetailAnalysisService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
        _personDetector = new Lazy<YoloOnnxDetector?>(() => CreateDetector(_options.ClientPresenceModelFileName, "person"));
        _phoneDetector = new Lazy<YoloOnnxDetector?>(() => CreateDetector(_options.PhoneModelFileName, "phone", fixedClassId: 0));
        _bottlesDetector = new Lazy<YoloOnnxDetector?>(() => CreateDetector(_options.BottlesModelFileName, "bottles", fixedClassId: 0));
    }

    public RetailSceneAnalysisResponse Analyze(RetailSceneAnalysisRequest request)
    {
        try
        {
            using var image = Image.Load<Rgb24>(request.FrameJpegBytes);

            var clientZones = request.Zones
                .Where(zone => string.Equals(zone.ZoneTypeKey, "client-zone", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var phoneZones = request.Zones
                .Where(zone => string.Equals(zone.ZoneTypeKey, "phone-zone", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var bottlesZones = request.Zones
                .Where(zone => string.Equals(zone.ZoneTypeKey, "bottles-zone", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var personDetections = _personDetector.Value?.Detect(image, _options.PresenceConfidenceThreshold, PersonClassId)
                ?? (_options.UseStubFallback
                    ? clientZones.Select(zone => CreateFallbackDetection(zone, "person", image.Width, image.Height)).ToArray()
                    : Array.Empty<YoloDetection>());
            var clientPeopleDetections = personDetections
                .Where(detection => clientZones.Any(zone => IsPointInsideZone(zone, detection.CenterX / image.Width, detection.CenterY / image.Height)))
                .ToArray();

            var phoneDetections = DetectInZones(
                image,
                phoneZones,
                _phoneDetector.Value,
                _options.PhoneConfidenceThreshold,
                "phone");
            var bottleDetections = DetectInZones(
                image,
                bottlesZones,
                _bottlesDetector.Value,
                _options.BottlesConfidenceThreshold,
                "bottles");

            var isSimulated = _personDetector.Value is null || _phoneDetector.Value is null || _bottlesDetector.Value is null;
            var note = isSimulated
                ? "ONNX detector fallback mode is active because one or more configured model files could not be loaded."
                : "Retail analysis executed with configured ONNX models.";

            return new RetailSceneAnalysisResponse
            {
                ClientZoneHasPeople = clientPeopleDetections.Length > 0,
                ClientZoneConfidence = clientPeopleDetections.Length > 0
                    ? clientPeopleDetections.Max(item => item.Confidence)
                    : 0,
                IsSimulated = isSimulated,
                Note = note,
                Detections = new[]
                {
                    BuildResponseDetection("phone", phoneDetections, image.Width, image.Height),
                    BuildResponseDetection("bottles", bottleDetections, image.Width, image.Height),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retail analysis failed for camera {CameraKey}", request.CameraKey);
            if (!_options.UseStubFallback)
            {
                throw;
            }

            return new RetailSceneAnalysisResponse
            {
                ClientZoneHasPeople = false,
                ClientZoneConfidence = 0,
                IsSimulated = true,
                Note = $"Retail analysis fallback activated after runtime error: {ex.Message}",
                Detections = Array.Empty<RetailAnalysisDetectionDto>(),
            };
        }
    }

    private RetailAnalysisDetectionDto BuildResponseDetection(
        string detectionTypeKey,
        IReadOnlyList<YoloDetection> detections,
        int imageWidth,
        int imageHeight)
    {
        var strongest = detections.OrderByDescending(item => item.Confidence).FirstOrDefault();
        return new RetailAnalysisDetectionDto
        {
            DetectionTypeKey = detectionTypeKey,
            IsDetected = strongest is not null,
            Confidence = strongest?.Confidence,
            EvidenceLabel = strongest is not null ? $"{detectionTypeKey}-onnx-hit" : null,
            BoundingBoxes = detections
                .Take(10)
                .Select(item => new ZoneBoundsDto
                {
                    X = item.X / imageWidth,
                    Y = item.Y / imageHeight,
                    Width = item.Width / imageWidth,
                    Height = item.Height / imageHeight,
                })
                .ToArray(),
        };
    }

    private IReadOnlyList<YoloDetection> DetectInZones(
        Image<Rgb24> image,
        IReadOnlyList<RetailAnalysisZoneDto> zones,
        YoloOnnxDetector? detector,
        double confidenceThreshold,
        string fallbackLabel)
    {
        if (zones.Count == 0)
        {
            return Array.Empty<YoloDetection>();
        }

        if (detector is null)
        {
            return _options.UseStubFallback
                ? zones.Select(zone => CreateFallbackDetection(zone, fallbackLabel, image.Width, image.Height)).ToArray()
                : Array.Empty<YoloDetection>();
        }

        var results = new List<YoloDetection>();
        foreach (var zone in zones)
        {
            var cropRectangle = BuildCropRectangle(zone.Bounds, image.Width, image.Height);
            if (cropRectangle.width <= 2 || cropRectangle.height <= 2)
            {
                continue;
            }

            using var crop = image.Clone(context => context.Crop(new Rectangle(cropRectangle.x, cropRectangle.y, cropRectangle.width, cropRectangle.height)));
            var detections = detector.Detect(crop, confidenceThreshold);
            foreach (var detection in detections)
            {
                results.Add(new YoloDetection
                {
                    Label = detection.Label,
                    ClassId = detection.ClassId,
                    Confidence = detection.Confidence,
                    X = cropRectangle.x + detection.X,
                    Y = cropRectangle.y + detection.Y,
                    Width = detection.Width,
                    Height = detection.Height,
                });
            }
        }

        return results;
    }

    private YoloDetection CreateFallbackDetection(
        RetailAnalysisZoneDto zone,
        string fallbackLabel,
        int imageWidth,
        int imageHeight)
    {
        var (x, y, width, height) = BuildCropRectangle(zone.Bounds, imageWidth, imageHeight);
        return new YoloDetection
        {
            Label = fallbackLabel,
            ClassId = 0,
            Confidence = 0.95,
            X = x,
            Y = y,
            Width = width,
            Height = height,
        };
    }

    private static (int x, int y, int width, int height) BuildCropRectangle(
        ZoneBoundsDto bounds,
        int imageWidth,
        int imageHeight)
    {
        var left = Math.Clamp((int)Math.Round(bounds.X * imageWidth), 0, imageWidth - 1);
        var top = Math.Clamp((int)Math.Round(bounds.Y * imageHeight), 0, imageHeight - 1);
        var right = Math.Clamp((int)Math.Round((bounds.X + bounds.Width) * imageWidth), left + 1, imageWidth);
        var bottom = Math.Clamp((int)Math.Round((bounds.Y + bounds.Height) * imageHeight), top + 1, imageHeight);
        return (left, top, right - left, bottom - top);
    }

    private static bool IsPointInsideZone(RetailAnalysisZoneDto zone, double normalizedX, double normalizedY)
    {
        if (zone.Points.Count < 3)
        {
            return normalizedX >= zone.Bounds.X
                && normalizedX <= zone.Bounds.X + zone.Bounds.Width
                && normalizedY >= zone.Bounds.Y
                && normalizedY <= zone.Bounds.Y + zone.Bounds.Height;
        }

        var inside = false;
        for (var i = 0; i < zone.Points.Count; i++)
        {
            var current = zone.Points[i];
            var previous = zone.Points[(i + zone.Points.Count - 1) % zone.Points.Count];
            var intersects = ((current.Y > normalizedY) != (previous.Y > normalizedY))
                && normalizedX < (previous.X - current.X) * (normalizedY - current.Y) / ((previous.Y - current.Y) + double.Epsilon) + current.X;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private YoloOnnxDetector? CreateDetector(string fileName, string label, int? fixedClassId = null)
    {
        var modelPath = Path.Combine(_environment.ContentRootPath, _options.ModelsRootPath, fileName);
        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Retail model file {ModelPath} was not found. Stub fallback may be used instead.", modelPath);
            return null;
        }

        return new YoloOnnxDetector(modelPath, label, _options.InputSize, _options.IouThreshold, fixedClassId);
    }

    public void Dispose()
    {
        if (_personDetector.IsValueCreated)
        {
            _personDetector.Value?.Dispose();
        }
        if (_phoneDetector.IsValueCreated)
        {
            _phoneDetector.Value?.Dispose();
        }
        if (_bottlesDetector.IsValueCreated)
        {
            _bottlesDetector.Value?.Dispose();
        }
    }
}
