namespace DicomViewer.Application.Models;

public sealed record PacsRemoteStudy(
    string RemoteStudyId,
    string StudyInstanceUid,
    string PatientId,
    string PatientName,
    string StudyDescription,
    string ModalitiesInStudy,
    int InstanceCount,
    DateTime? StudyDateUtc);