namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示查看器当前的缩放、平移、旋转和翻转状态。
/// </summary>
public sealed record ViewTransform(
    double Zoom,
    double PanX,
    double PanY,
    double RotationDegrees,
    bool FlipHorizontal,
    bool FlipVertical)
{
    /// <summary>
    /// 视口变换的默认初始值。
    /// </summary>
    public static ViewTransform Default { get; } = new(1.0, 0.0, 0.0, 0.0, false, false);
}