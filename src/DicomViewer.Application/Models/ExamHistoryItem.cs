using DicomViewer.Domain.Enums;

namespace DicomViewer.Application.Models;

public sealed record ExamHistoryItem(
    string SessionId,
    string PatientName,
    string ProcedureDescription,
    string BodyPart,
    string Projection,
    ExamWorkflowStatus WorkflowStatus,
    DeviceOperationalState DeviceState,
    DateTime UpdatedAtUtc,
    string? ArtifactPath);