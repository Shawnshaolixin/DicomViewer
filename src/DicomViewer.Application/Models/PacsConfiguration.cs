namespace DicomViewer.Application.Models;

public sealed record PacsConfiguration(
    string CallingAeTitle,
    string CalledAeTitle,
    string Host,
    int Port,
    string OutputDirectory)
{
    public static PacsConfiguration Default { get; } = new(
        "DICOMVIEWER",
        "ORTHANC",
        "127.0.0.1",
        4242,
        Path.Combine(AppContext.BaseDirectory, "simulated-output"));
}