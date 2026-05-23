namespace DicomViewer.Domain.Enums;

public enum InterlockCode
{
    None = 0,
    NoActiveOrder = 1,
    DetectorDisconnected = 2,
    TubeNotWarmedUp = 3,
    DoorOpen = 4,
    ParameterOutOfRange = 5,
    PacsUnavailable = 6,
}