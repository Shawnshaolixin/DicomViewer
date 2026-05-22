using DicomViewer.Rendering.Abstractions;

namespace DicomViewer.Rendering.Services;

public sealed class PlaceholderRenderService : IImageRenderService
{
    public RenderedViewport Render(RenderRequest request)
    {
        // 当前渲染服务主要负责生成视口说明文本；真实位图由 Infrastructure 的图像服务提供。
        var title = $"{request.Series.Modality} | {request.Series.SeriesDescription}";
        var subtitle = $"Slice {request.SliceIndex + 1}/{request.SliceCount} | Frame {request.FrameIndex + 1}/{request.FrameCount} | {request.WindowLevel}";
        var placeholderText = $"{request.Image.Width} x {request.Image.Height}\nTool: {request.ToolMode}\nZoom: {request.ViewTransform.Zoom:0.00}x";
        var statusText = $"Viewport ready: {request.Series.SeriesInstanceUid} | Frame {request.FrameIndex + 1}/{request.FrameCount}";

        return new RenderedViewport(title, subtitle, placeholderText, statusText);
    }
}