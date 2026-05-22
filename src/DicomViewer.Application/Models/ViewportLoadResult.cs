namespace DicomViewer.Application.Models;

public sealed record ViewportLoadResult(
    ViewportImageData? Image,
    string Message)
{
    public bool Succeeded => Image is not null;
}
