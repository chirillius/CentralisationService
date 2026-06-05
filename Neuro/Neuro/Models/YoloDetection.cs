namespace Neuro.Models;

public sealed class YoloDetection
{
    public required string Label { get; init; }
    public required int ClassId { get; init; }
    public required double Confidence { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public double CenterX => X + Width / 2d;
    public double CenterY => Y + Height / 2d;
}
