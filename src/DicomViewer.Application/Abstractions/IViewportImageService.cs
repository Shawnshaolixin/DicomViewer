using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Abstractions;

public interface IViewportImageService
{
    ViewportLoadResult TryLoad(string filePath, int frameIndex, WindowLevel windowLevel);
}