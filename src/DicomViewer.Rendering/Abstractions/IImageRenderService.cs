namespace DicomViewer.Rendering.Abstractions;

/// <summary>
/// 定义视口说明信息的渲染入口。
/// </summary>
public interface IImageRenderService
{
    /// <summary>
    /// 根据当前序列、图像和视图状态生成标题、副标题与占位说明文本。
    /// </summary>
    RenderedViewport Render(RenderRequest request);
}