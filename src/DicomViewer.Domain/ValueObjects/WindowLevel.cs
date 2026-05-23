namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示图像显示所需的窗宽和窗位。
/// </summary>
public sealed record WindowLevel(double Width, double Center)
{
    /// <summary>
    /// 以查看器常用格式输出窗宽窗位文本。
    /// </summary>
    public override string ToString()
    {
        return $"WW {Width:0} / WL {Center:0}";
    }
}