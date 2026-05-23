namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示一次影像检查，包含多个序列。
/// </summary>
public sealed class Study
{
    public required string StudyInstanceUid { get; init; }

    public required string StudyDescription { get; init; }

    public DateTime? StudyDate { get; init; }

    public IReadOnlyList<Series> SeriesList { get; init; } = Array.Empty<Series>();
}