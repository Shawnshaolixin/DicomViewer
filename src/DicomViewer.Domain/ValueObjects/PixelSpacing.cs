namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示图像像素在行和列方向上的物理间距，单位通常为毫米。
/// </summary>
public sealed record PixelSpacing(double Row, double Column);