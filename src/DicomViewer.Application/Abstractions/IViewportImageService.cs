using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Abstractions;

public interface IViewportImageService
{
    ViewportImageData? TryLoad(string filePath, int frameIndex, WindowLevel windowLevel);
}