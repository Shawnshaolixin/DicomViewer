using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示序列中的一个 DICOM 图像实例或多帧对象。
/// </summary>
public sealed class ImageInstance
{
    public required string SopInstanceUid { get; init; }

    public required string FilePath { get; init; }

    public int InstanceNumber { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int FrameCount { get; init; }

    public required PixelSpacing PixelSpacing { get; init; }

    public required WindowLevel DefaultWindowLevel { get; init; }
}