namespace DicomViewer.Application.Models;

public sealed record WorkspaceSnapshot(
    IReadOnlyList<SeriesSummary> SeriesItems,
    string ActiveSeriesInstanceUid,
    ViewportImageData? ViewportImage,
    string ViewerTitle,
    string ViewerSubtitle,
    string PlaceholderText,
    string StatusText,
    string PatientText,
    string StudyText,
    string ToolText,
    string WindowText,
    string SliceText,
    string ViewText,
    string NotesText);