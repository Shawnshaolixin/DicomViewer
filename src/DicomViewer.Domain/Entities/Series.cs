using DicomViewer.Domain.Enums;

namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示检查中的一个影像序列，通常按模态和采集条件组织多个实例。
/// </summary>
public sealed class Series
{
    public required string SeriesInstanceUid { get; init; }

    public required string SeriesDescription { get; init; }

    public required ModalityType Modality { get; init; }

    public IReadOnlyList<ImageInstance> Instances { get; init; } = Array.Empty<ImageInstance>();
}