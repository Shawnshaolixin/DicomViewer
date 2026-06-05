namespace DicomViewer.WorkflowScpDemo;

internal sealed class WorkflowDataFile
{
    public List<WorklistItemRecord> WorklistItems { get; init; } = [];

    public List<MppsRecord> MppsRecords { get; init; } = [];
}

internal sealed class WorklistItemRecord
{
    public string OrderId { get; init; } = string.Empty;

    public string PatientId { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string AccessionNumber { get; init; } = string.Empty;

    public string RequestedProcedureId { get; init; } = string.Empty;

    public string ScheduledProcedureStepId { get; init; } = string.Empty;

    public string StudyInstanceUid { get; init; } = string.Empty;

    public string Modality { get; init; } = "DX";

    public string ScheduledStationAeTitle { get; init; } = "DICOMVIEWER";

    public string ScheduledProcedureStepDescription { get; init; } = string.Empty;

    public string RequestedProcedureDescription { get; init; } = string.Empty;

    public string ReferringPhysicianName { get; init; } = string.Empty;

    public string PatientSex { get; init; } = string.Empty;

    public string PatientBirthDate { get; init; } = string.Empty;

    public string Status { get; set; } = "SCHEDULED";

    public DateTime ScheduledStart { get; init; }
}

internal sealed class MppsRecord
{
    public string SopInstanceUid { get; init; } = string.Empty;

    public string PerformedProcedureStepId { get; init; } = string.Empty;

    public string ScheduledProcedureStepId { get; init; } = string.Empty;

    public string AccessionNumber { get; init; } = string.Empty;

    public string StudyInstanceUid { get; init; } = string.Empty;

    public string PatientId { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string Status { get; set; } = "IN PROGRESS";

    public string? Comments { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class WorklistQueryCriteria
{
    public string PatientId { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string AccessionNumber { get; init; } = string.Empty;

    public string Modality { get; init; } = string.Empty;

    public string ScheduledStationAeTitle { get; init; } = string.Empty;

    public string ScheduledDateRange { get; init; } = string.Empty;
}

internal sealed record MppsUpsertResult(bool Success, WorklistItemRecord? WorklistItem, MppsRecord? Record, string Message);
