namespace DicomViewer.Application.Models;

public sealed record ViewportImageData(
    byte[] Pixels,
    int Width,
    int Height,
    int Stride);