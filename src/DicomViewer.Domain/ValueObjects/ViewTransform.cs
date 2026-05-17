namespace DicomViewer.Domain.ValueObjects;

public sealed record ViewTransform(
    double Zoom,
    double PanX,
    double PanY,
    double RotationDegrees,
    bool FlipHorizontal,
    bool FlipVertical)
{
    public static ViewTransform Default { get; } = new(1.0, 0.0, 0.0, 0.0, false, false);
}