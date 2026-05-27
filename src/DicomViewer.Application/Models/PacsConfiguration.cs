namespace DicomViewer.Application.Models;

public sealed record PacsConfiguration(
    string CallingAeTitle,
    string CalledAeTitle,
    string Host,
    int Port,
    int RestApiPort,
    string OutputDirectory,
    string LocalStoreHost = "127.0.0.1",
    int LocalStorePort = 11113,
    string MwlCalledAeTitle = "ORTHANC",
    string MwlHost = "127.0.0.1",
    int MwlPort = 4242,
    string MppsCalledAeTitle = "ORTHANC",
    string MppsHost = "127.0.0.1",
    int MppsPort = 4242,
    string StationAeTitle = "DICOMVIEWER",
    string StationName = "DICOM Viewer Console")
{
    public static PacsConfiguration Default { get; } = new(
        "DICOMVIEWER",
        "ORTHANC",
        "127.0.0.1",
        4242,
        8042,
        Path.Combine(AppContext.BaseDirectory, "simulated-output"),
        "127.0.0.1",
        11113,
        "ORTHANC",
        "127.0.0.1",
        4242,
        "ORTHANC",
        "127.0.0.1",
        4242,
        "DICOMVIEWER",
        "DICOM Viewer Console");
}