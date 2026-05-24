using DicomViewer.Domain.Enums;

namespace DicomViewer.Application.Models;

public sealed record ExamSessionRecord(
    string SessionId,
    string OrderId,
    string PatientId,
    string PatientName,
    string ProcedureDescription,
    string BodyPart,
    string Projection,
    ExamWorkflowStatus WorkflowStatus,
    DeviceOperationalState DeviceState,
    DateTime StartedAtUtc,
    DateTime? LastExposureAtUtc,
    string? LastGeneratedArtifactPath,
    string? LastImageId,
    DateTime UpdatedAtUtc);