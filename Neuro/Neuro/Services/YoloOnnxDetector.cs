using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Neuro.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Neuro.Services;

public sealed class YoloOnnxDetector : IDisposable
{
    private sealed record PreparedImage(
        DenseTensor<float> Tensor,
        int OriginalWidth,
        int OriginalHeight,
        float Scale,
        float PadX,
        float PadY);

    private readonly InferenceSession _session;
    private readonly string _label;
    private readonly int? _fixedClassId;
    private readonly int _inputSize;
    private readonly double _iouThreshold;
    private readonly string _inputName;
    private readonly string _outputName;

    public YoloOnnxDetector(
        string modelPath,
        string label,
        int inputSize,
        double iouThreshold,
        int? fixedClassId = null)
    {
        var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _session = new InferenceSession(modelPath, sessionOptions);
        _label = label;
        _fixedClassId = fixedClassId;
        _inputSize = inputSize;
        _iouThreshold = iouThreshold;
        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();
    }

    public IReadOnlyList<YoloDetection> Detect(
        Image<Rgb24> image,
        double confidenceThreshold,
        int? preferredClassId = null)
    {
        var prepared = PrepareImage(image);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, prepared.Tensor),
        };
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
        var output = results.First(item => item.Name == _outputName).AsTensor<float>();
        return DecodeDetections(output, prepared, confidenceThreshold, preferredClassId ?? _fixedClassId);
    }

    private PreparedImage PrepareImage(Image<Rgb24> image)
    {
        var tensor = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });
        var scale = Math.Min((float)_inputSize / image.Width, (float)_inputSize / image.Height);
        var resizedWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
        var resizedHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
        var padX = (_inputSize - resizedWidth) / 2f;
        var padY = (_inputSize - resizedHeight) / 2f;

        using var resized = image.Clone(context => context.Resize(resizedWidth, resizedHeight));
        for (var y = 0; y < resizedHeight; y++)
        {
            var targetY = (int)padY + y;
            for (var x = 0; x < resizedWidth; x++)
            {
                var pixel = resized[x, y];
                var targetX = (int)padX + x;
                tensor[0, 0, targetY, targetX] = pixel.R / 255f;
                tensor[0, 1, targetY, targetX] = pixel.G / 255f;
                tensor[0, 2, targetY, targetX] = pixel.B / 255f;
            }
        }

        return new PreparedImage(tensor, image.Width, image.Height, scale, padX, padY);
    }

    private IReadOnlyList<YoloDetection> DecodeDetections(
        Tensor<float> output,
        PreparedImage prepared,
        double confidenceThreshold,
        int? preferredClassId)
    {
        var channels = output.Dimensions[1];
        var candidates = output.Dimensions[2];
        var rawDetections = new List<YoloDetection>();

        for (var candidateIndex = 0; candidateIndex < candidates; candidateIndex++)
        {
            var classId = preferredClassId ?? 0;
            float confidence;

            if (channels <= 5)
            {
                confidence = output[0, 4, candidateIndex];
            }
            else
            {
                confidence = 0;
                for (var classIndex = 4; classIndex < channels; classIndex++)
                {
                    var classConfidence = output[0, classIndex, candidateIndex];
                    if (preferredClassId.HasValue)
                    {
                        if (classIndex - 4 != preferredClassId.Value)
                        {
                            continue;
                        }
                        confidence = classConfidence;
                        classId = preferredClassId.Value;
                        break;
                    }

                    if (classConfidence > confidence)
                    {
                        confidence = classConfidence;
                        classId = classIndex - 4;
                    }
                }
            }

            if (confidence < confidenceThreshold)
            {
                continue;
            }

            var centerX = output[0, 0, candidateIndex];
            var centerY = output[0, 1, candidateIndex];
            var width = output[0, 2, candidateIndex];
            var height = output[0, 3, candidateIndex];

            var left = (centerX - width / 2f - prepared.PadX) / prepared.Scale;
            var top = (centerY - height / 2f - prepared.PadY) / prepared.Scale;
            var right = (centerX + width / 2f - prepared.PadX) / prepared.Scale;
            var bottom = (centerY + height / 2f - prepared.PadY) / prepared.Scale;

            left = Math.Clamp(left, 0, prepared.OriginalWidth);
            top = Math.Clamp(top, 0, prepared.OriginalHeight);
            right = Math.Clamp(right, 0, prepared.OriginalWidth);
            bottom = Math.Clamp(bottom, 0, prepared.OriginalHeight);

            if (right <= left || bottom <= top)
            {
                continue;
            }

            rawDetections.Add(new YoloDetection
            {
                Label = _label,
                ClassId = classId,
                Confidence = confidence,
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top,
            });
        }

        return ApplyNonMaximumSuppression(rawDetections);
    }

    private IReadOnlyList<YoloDetection> ApplyNonMaximumSuppression(List<YoloDetection> detections)
    {
        var ordered = detections
            .OrderByDescending(item => item.Confidence)
            .ToList();
        var selected = new List<YoloDetection>();

        while (ordered.Count > 0)
        {
            var current = ordered[0];
            ordered.RemoveAt(0);
            selected.Add(current);
            ordered.RemoveAll(candidate => ComputeIoU(current, candidate) >= _iouThreshold);
        }

        return selected;
    }

    private static double ComputeIoU(YoloDetection left, YoloDetection right)
    {
        var intersectionLeft = Math.Max(left.X, right.X);
        var intersectionTop = Math.Max(left.Y, right.Y);
        var intersectionRight = Math.Min(left.X + left.Width, right.X + right.Width);
        var intersectionBottom = Math.Min(left.Y + left.Height, right.Y + right.Height);

        if (intersectionRight <= intersectionLeft || intersectionBottom <= intersectionTop)
        {
            return 0;
        }

        var intersectionArea = (intersectionRight - intersectionLeft) * (intersectionBottom - intersectionTop);
        var unionArea = left.Width * left.Height + right.Width * right.Height - intersectionArea;
        return unionArea <= 0 ? 0 : intersectionArea / unionArea;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
