using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Models;

public sealed record ConsoleSnapshot(
    IReadOnlyList<WorklistItem> WorklistItems,
    string? SelectedOrderId,
    ExposureParameters ExposureParameters,
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