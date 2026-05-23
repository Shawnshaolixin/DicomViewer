namespace DicomViewer.Application.Models;

public sealed record ExposureResult(
    string ImageId,
    string PreviewText,
    string ArtifactPath,
    DateTime AcquiredAtUtc);