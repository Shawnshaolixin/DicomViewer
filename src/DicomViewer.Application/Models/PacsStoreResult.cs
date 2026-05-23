namespace DicomViewer.Application.Models;

public sealed record PacsStoreResult(
    bool IsSuccess,
    string StatusText,
    string Details,
    string CalledAeTitle,
    string Host,
    int Port,
    string FilePath,
    DateTime ProcessedAtUtc);