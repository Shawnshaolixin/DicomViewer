namespace DicomViewer.Application.Models;

public sealed record PacsStudyQueryCriteria(
    string PatientName,
    string PatientId,
    string StudyDescription,
    string Modality,
    DateTime? StudyDateFromUtc,
    DateTime? StudyDateToUtc)
{
    public static PacsStudyQueryCriteria Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, null, null);
}