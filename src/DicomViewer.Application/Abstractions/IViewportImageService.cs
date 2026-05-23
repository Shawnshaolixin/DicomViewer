using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 提供查看器视口所需的像素加载与转换能力。
/// </summary>
public interface IViewportImageService
{
    /// <summary>
    /// 读取指定图像帧并转换为查看器可显示的数据。
    /// </summary>
    ViewportLoadResult TryLoad(string filePath, int frameIndex, WindowLevel windowLevel);
}