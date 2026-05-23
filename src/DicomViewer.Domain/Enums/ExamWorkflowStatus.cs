namespace DicomViewer.Domain.Enums;

public enum ExamWorkflowStatus
{
    Scheduled = 0,
    InProgress = 1,
    Ready = 2,
    Acquiring = 3,
    Processing = 4,
    Sending = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,
}