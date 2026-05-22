using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Models;

public sealed record StudyCatalogLoadResult(
    IReadOnlyList<Patient> Patients,
    string StatusText,
    string NoteText,
    int ScannedFileCount,
    int ImportedFileCount,
    int SkippedFileCount,
    bool IsSampleData);
