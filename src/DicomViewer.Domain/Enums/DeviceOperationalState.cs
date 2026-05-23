namespace DicomViewer.Domain.Enums;

public enum DeviceOperationalState
{
    Offline = 0,
    Idle = 1,
    Preparing = 2,
    Ready = 3,
    Exposing = 4,
    Processing = 5,
    Error = 6,
}