namespace DicomViewer.Application.Models;

public sealed record PacsConfiguration(
    string CallingAeTitle,
    string CalledAeTitle,
    string Host,
    int Port,
    int RestApiPort,
    string OutputDirectory,
    string LocalStoreHost = "127.0.0.1",
    int LocalStorePort = 11113)
{
    public static PacsConfiguration Default { get; } = new(
        "DICOMVIEWER",
        "ORTHANC",
        "127.0.0.1",
        4242,
        8042,
        Path.Combine(AppContext.BaseDirectory, "simulated-output"),
        "127.0.0.1",
        11113);
}