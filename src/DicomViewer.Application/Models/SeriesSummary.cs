using DicomViewer.Domain.Enums;

namespace DicomViewer.Application.Models;

public sealed record SeriesSummary(
    string SeriesInstanceUid,
    string DisplayName,
    ModalityType Modality,
    int ImageCount,
    ViewportImageData? ThumbnailImage = null);