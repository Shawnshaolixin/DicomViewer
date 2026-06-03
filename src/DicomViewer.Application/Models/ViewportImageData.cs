namespace DicomViewer.Application.Models;

public sealed record ViewportImageData(
    byte[] Pixels,
    int Width,
    int Height,
    int Stride,
    ViewportPixelFormat PixelFormat = ViewportPixelFormat.Gray8);