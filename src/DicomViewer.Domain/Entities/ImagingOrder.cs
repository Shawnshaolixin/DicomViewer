namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示工作列表中的一条检查申请单。
/// </summary>
public sealed class ImagingOrder
{
    public required string OrderId { get; init; }

    public required string PatientId { get; init; }

    public required string PatientName { get; init; }

    public string AccessionNumber { get; init; } = string.Empty;

    public string RequestedProcedureId { get; init; } = string.Empty;

    public string ScheduledProcedureStepId { get; init; } = string.Empty;

    public string StudyInstanceUid { get; init; } = string.Empty;

    public string Modality { get; init; } = string.Empty;

    public string ScheduledStationAeTitle { get; init; } = string.Empty;

    public DateTime? ScheduledStartDateTime { get; init; }

    public string ReferringPhysicianName { get; init; } = string.Empty;

    public string PatientSex { get; init; } = string.Empty;

    public DateTime? PatientBirthDate { get; init; }

    public string RequestedProcedureDescription { get; init; } = string.Empty;

    public string SourceType { get; init; } = "Mock";

    public required string ProcedureDescription { get; init; }

    public required string BodyPart { get; init; }

    public required string Projection { get; init; }

    public required DateTime ScheduledTime { get; init; }

    public string Status { get; init; } = "Scheduled";
}