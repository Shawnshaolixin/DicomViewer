namespace DicomViewer.Application.Models;

public sealed record PacsStudyQueryResult(
    bool IsSuccess,
    string StatusText,
    string Details,
    IReadOnlyList<PacsRemoteStudy> Studies,
    DateTime ProcessedAtUtc);