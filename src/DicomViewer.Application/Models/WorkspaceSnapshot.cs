using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Models;

public sealed record WorkspaceSnapshot(
    IReadOnlyList<SeriesSummary> SeriesItems,
    string ActiveSeriesInstanceUid,
    ViewportImageData? ViewportImage,
    ViewTransform ViewTransform,
    ViewerToolMode ToolMode,
    string ViewerTitle,
    string ViewerSubtitle,
    string PlaceholderText,
    string StatusText,
    string PatientText,
    string StudyText,
    string ToolText,
    string WindowText,
    string SliceText,
    string FrameText,
    string ViewText,
    string NotesText,
    IReadOnlyList<MeasurementAnnotation> Measurements);