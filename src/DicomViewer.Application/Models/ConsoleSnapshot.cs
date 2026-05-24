using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Models;

public sealed record ConsoleSnapshot(
    IReadOnlyList<WorklistItem> WorklistItems,
    IReadOnlyList<ExamHistoryItem> HistoryItems,
    IReadOnlyList<PacsRemoteStudy> RemoteStudies,
    string? SelectedOrderId,
    ExposureParameters ExposureParameters,
    ExposureParameterRange ExposureParameterRange,
    PacsConfiguration PacsConfiguration,
    DeviceOperationalState DeviceState,
    ExamWorkflowStatus? WorkflowStatus,
    bool CanExpose,
    string CurrentPatientText,
    string CurrentOrderText,
    string StatusText,
    string NotesText,
    IReadOnlyList<string> InterlockMessages,
    IReadOnlyList<string> AuditEntries,
    ExposureResult? LastExposureResult,
    PacsStoreResult? LastPacsStoreResult);