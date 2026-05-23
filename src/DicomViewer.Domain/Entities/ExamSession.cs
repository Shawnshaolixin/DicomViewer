using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Domain.Entities;

public sealed record ExamSession(
    string SessionId,
    ImagingOrder Order,
    ExposureParameters ExposureParameters,
    ExamWorkflowStatus WorkflowStatus,
    DeviceOperationalState DeviceState,
    DateTime StartedAtUtc,
    DateTime? LastExposureAtUtc,
    string? LastGeneratedArtifact);