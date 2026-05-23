using DicomViewer.Rendering.Abstractions;

namespace DicomViewer.Rendering.Services;

/// <summary>
/// 生成查看器标题、状态栏和占位文本。
/// 该服务不负责真实像素渲染，只负责组织界面展示所需的说明信息。
/// </summary>
public sealed class PlaceholderRenderService : IImageRenderService
{
    /// <summary>
    /// 根据渲染请求生成视口标题、副标题、占位文本和状态栏文本。
    /// </summary>
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