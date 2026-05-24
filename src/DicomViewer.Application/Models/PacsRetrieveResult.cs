namespace DicomViewer.Application.Models;

public sealed record PacsRetrieveResult(
    bool IsSuccess,
    string StatusText,
    string Details,
    string ImportedDirectoryPath,
    DateTime ProcessedAtUtc);