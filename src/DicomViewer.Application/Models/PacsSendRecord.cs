namespace DicomViewer.Application.Models;

public sealed record PacsSendRecord(
    string SessionId,
    string FilePath,
    bool IsSuccess,
    string StatusText,
    string Details,
    string CalledAeTitle,
    string Host,
    int Port,
    DateTime ProcessedAtUtc);