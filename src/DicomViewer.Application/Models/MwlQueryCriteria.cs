namespace DicomViewer.Application.Models;

public sealed record MwlQueryCriteria(
    string PatientId,
    string PatientName,
    string AccessionNumber,
    string Modality,
    DateTime? ScheduledDateFrom,
    DateTime? ScheduledDateTo,
    string ScheduledStationAeTitle)
{
    public static MwlQueryCriteria Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        null,
        string.Empty);
}