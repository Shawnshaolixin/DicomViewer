using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Models;

public sealed record MeasurementAnnotation(
    Guid Id,
    string Label,
    IReadOnlyList<Point2D> Points,
    bool IsPreview = false);
