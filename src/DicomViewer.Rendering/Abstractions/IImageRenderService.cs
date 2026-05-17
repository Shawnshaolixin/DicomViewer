namespace DicomViewer.Rendering.Abstractions;

public interface IImageRenderService
{
    RenderedViewport Render(RenderRequest request);
}