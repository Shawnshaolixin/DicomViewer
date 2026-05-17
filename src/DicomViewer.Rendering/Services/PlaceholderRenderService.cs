using DicomViewer.Rendering.Abstractions;

namespace DicomViewer.Rendering.Services;

public sealed class PlaceholderRenderService : IImageRenderService
{
    public RenderedViewport Render(RenderRequest request)
    {
        var title = $"{request.Series.Modality} | {request.Series.SeriesDescription}";
        var subtitle = $"Slice {request.SliceIndex + 1}/{request.SliceCount} | {request.WindowLevel}";
        var placeholderText = $"{request.Image.Width} x {request.Image.Height}\nTool: {request.ToolMode}\nZoom: {request.ViewTransform.Zoom:0.00}x";
        var statusText = $"Viewport ready: {request.Series.SeriesInstanceUid}";

        return new RenderedViewport(title, subtitle, placeholderText, statusText);
    }
}