namespace DicomViewer.Infrastructure.Pacs;

public sealed class LocalDicomReceiveSession
{
    private readonly List<string> _receivedFiles = [];

    public LocalDicomReceiveSession(string targetDirectory)
    {
        TargetDirectory = targetDirectory;
    }

    public string TargetDirectory { get; }

    public IReadOnlyList<string> ReceivedFiles => _receivedFiles;

    internal void AddReceivedFile(string filePath)
    {
        _receivedFiles.Add(filePath);
    }
}