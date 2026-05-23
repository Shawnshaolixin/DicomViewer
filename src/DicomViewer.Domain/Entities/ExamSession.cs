using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示一次检查从准备、曝光到发送的运行时会话状态。
/// </summary>
public sealed record ExamSession(
    string SessionId,
    ImagingOrder Order,
    ExposureParameters ExposureParameters,
    ExamWorkflowStatus WorkflowStatus,
    DeviceOperationalState DeviceState,
    DateTime StartedAtUtc,
    DateTime? LastExposureAtUtc,
    string? LastGeneratedArtifact);