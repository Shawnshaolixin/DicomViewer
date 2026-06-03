using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Rendering;
using DicomViewer.Rendering.Abstractions;

namespace DicomViewer.Tests.Application;

public sealed class WorkspaceServiceTests
{
    [Fact]
    public async Task MoveFrame_WithMultiFrameInstance_UpdatesFrameTextAndImage()
    {
        var service = CreateWorkspaceService();

        _ = await service.LoadAsync();
        var snapshot = service.MoveFrame(1);

        Assert.Equal("Frame 2 / 3", snapshot.FrameText);
        Assert.NotNull(snapshot.ViewportImage);
        Assert.Equal(1, snapshot.ViewportImage!.Pixels[0]);
    }

    [Fact]
    public async Task AddMeasurementPoint_WithLengthTool_CreatesMeasurementOverlay()
    {
        var service = CreateWorkspaceService();

        _ = await service.LoadAsync();
        _ = service.SetTool(ViewerToolMode.MeasureLength);
        _ = service.AddMeasurementPoint(new Point2D(0, 0));
        var snapshot = service.AddMeasurementPoint(new Point2D(10, 0));

        var measurement = Assert.Single(snapshot.Measurements);
        Assert.Equal("10.0 mm", measurement.Label);
        Assert.False(measurement.IsPreview);
    }

    [Fact]
    public async Task RemoveMeasurement_WithExistingMeasurement_RemovesOnlySelectedMeasurement()
    {
        var service = CreateWorkspaceService();

        _ = await service.LoadAsync();
        _ = service.SetTool(ViewerToolMode.MeasureLength);
        _ = service.AddMeasurementPoint(new Point2D(0, 0));
        var firstSnapshot = service.AddMeasurementPoint(new Point2D(10, 0));
        _ = service.AddMeasurementPoint(new Point2D(0, 0));
        var secondSnapshot = service.AddMeasurementPoint(new Point2D(0, 12));

        var snapshot = service.RemoveMeasurement(firstSnapshot.Measurements[0].Id);

        var measurement = Assert.Single(snapshot.Measurements);
        Assert.Equal(secondSnapshot.Measurements[1].Id, measurement.Id);
        Assert.Equal("12.0 mm", measurement.Label);
    }

    [Fact]
    public async Task RotateAndFlip_UpdatesViewTransformAndViewText()
    {
        var service = CreateWorkspaceService();

        _ = await service.LoadAsync();
        _ = service.Rotate(90);
        _ = service.ToggleFlipHorizontal();
        var snapshot = service.ToggleFlipVertical();

        Assert.Equal(90, snapshot.ViewTransform.RotationDegrees);
        Assert.True(snapshot.ViewTransform.FlipHorizontal);
        Assert.True(snapshot.ViewTransform.FlipVertical);
        Assert.Contains("Rot 90°", snapshot.ViewText);
        Assert.Contains("Flip H", snapshot.ViewText);
        Assert.Contains("Flip V", snapshot.ViewText);
    }

    [Fact]
    public async Task LoadAsync_WhenImportFails_ReportsStatus()
    {
        var service = new WorkspaceService(
            new FixedStudyCatalogService(new StudyCatalogLoadResult(
                Array.Empty<Patient>(),
                "Import failed",
                "目录不存在: /missing",
                0,
                0,
                0,
                false)),
            new FakeRenderService(),
            new FakeViewportImageService());

        var snapshot = await service.LoadAsync("/missing");

        Assert.Equal("Workspace is empty", snapshot.StatusText);
        Assert.Equal("目录不存在: /missing", snapshot.NotesText);
    }

    [Fact]
    public async Task LoadAsync_BuildsThumbnailForSeriesList()
    {
        var service = CreateWorkspaceService();

        var snapshot = await service.LoadAsync();

        var series = Assert.Single(snapshot.SeriesItems);
        Assert.NotNull(series.ThumbnailImage);
        Assert.Equal(2, series.ThumbnailImage!.Width);
        Assert.Equal(2, series.ThumbnailImage.Height);
    }

    private static WorkspaceService CreateWorkspaceService()
    {
        var imageInstance = new ImageInstance
        {
            SopInstanceUid = "instance-1",
            FilePath = "frame-test.dcm",
            InstanceNumber = 1,
            Width = 32,
            Height = 32,
            FrameCount = 3,
            PixelSpacing = new PixelSpacing(1, 1),
            DefaultWindowLevel = new WindowLevel(255, 127.5),
        };

        var series = new Series
        {
            SeriesInstanceUid = "series-1",
            SeriesDescription = "Demo Series",
            Modality = ModalityType.CT,
            Instances = new[] { imageInstance },
        };

        var study = new Study
        {
            StudyInstanceUid = "study-1",
            StudyDescription = "Demo Study",
            SeriesList = new[] { series },
        };

        var patient = new Patient
        {
            PatientId = "patient-1",
            PatientName = "Demo Patient",
            Studies = new[] { study },
        };

        return new WorkspaceService(
            new FixedStudyCatalogService(new StudyCatalogLoadResult(
                new[] { patient },
                "Loaded",
                "已加载测试数据。",
                1,
                1,
                0,
                false)),
            new FakeRenderService(),
            new FakeViewportImageService());
    }

    private sealed class FixedStudyCatalogService : IStudyCatalogService
    {
        private readonly StudyCatalogLoadResult _result;

        public FixedStudyCatalogService(StudyCatalogLoadResult result)
        {
            _result = result;
        }

        public Task<StudyCatalogLoadResult> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeViewportImageService : IViewportImageService
    {
        public ViewportLoadResult TryLoad(string filePath, int frameIndex, WindowLevel windowLevel)
        {
            return new ViewportLoadResult(new ViewportImageData(new byte[] { (byte)frameIndex, 1, 2, 3 }, 2, 2, 2), $"frame {frameIndex + 1}");
        }
    }

    private sealed class FakeRenderService : IImageRenderService
    {
        public RenderedViewport Render(RenderRequest request)
        {
            return new RenderedViewport("title", "subtitle", "placeholder", $"Frame {request.FrameIndex + 1}");
        }
    }
}
