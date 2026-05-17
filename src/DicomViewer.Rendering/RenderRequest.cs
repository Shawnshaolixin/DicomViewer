using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Rendering;

public sealed record RenderRequest(
    Series Series,
    ImageInstance Image,
    int SliceIndex,
    int SliceCount,
    ViewerToolMode ToolMode,
    WindowLevel WindowLevel,
    ViewTransform ViewTransform);