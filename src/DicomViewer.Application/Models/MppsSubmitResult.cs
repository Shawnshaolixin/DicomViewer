namespace DicomViewer.Application.Models;

public sealed record MppsSubmitResult(
    bool IsSuccess,
    string StatusText,
    string Details,
    string SopInstanceUid,
    DateTime ProcessedAtUtc);