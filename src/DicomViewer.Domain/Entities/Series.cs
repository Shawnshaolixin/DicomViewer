using DicomViewer.Domain.Enums;

namespace DicomViewer.Domain.Entities;

public sealed class Series
{
    public required string SeriesInstanceUid { get; init; }

    public required string SeriesDescription { get; init; }

    public required ModalityType Modality { get; init; }

    public IReadOnlyList<ImageInstance> Instances { get; init; } = Array.Empty<ImageInstance>();
}