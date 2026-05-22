using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Rendering;
using DicomViewer.Rendering.Abstractions;

namespace DicomViewer.Application.Services;

public sealed class WorkspaceService
{
    private const double MinimumVectorMagnitude = 1e-10;

    private readonly IStudyCatalogService _studyCatalogService;
    private readonly IImageRenderService _imageRenderService;
    private readonly IViewportImageService _viewportImageService;

    private readonly Dictionary<string, List<MeasurementAnnotation>> _measurementsBySeries = new();

    private IReadOnlyList<Patient> _patients = Array.Empty<Patient>();
    private IReadOnlyList<Series> _seriesList = Array.Empty<Series>();
    private int _activeSeriesIndex;
    private int _activeSliceIndex;
    private int _activeFrameIndex;
    private ViewerToolMode _toolMode = ViewerToolMode.Pan;
    private ViewTransform _viewTransform = ViewTransform.Default;
    private WindowLevel? _activeWindowLevel;
    private MeasurementDraft? _measurementDraft;
    private string _workspaceNote = "当前显示内置样例数据。";
    private string _workspaceStatus = "Sample workspace loaded";

    public WorkspaceService(
        IStudyCatalogService studyCatalogService,
        IImageRenderService imageRenderService,
        IViewportImageService viewportImageService)
    {
        _studyCatalogService = studyCatalogService;
        _imageRenderService = imageRenderService;
        _viewportImageService = viewportImageService;
    }

    public async Task<WorkspaceSnapshot> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default)
    {
        var loadResult = await _studyCatalogService.LoadAsync(sourcePath, cancellationToken);
        _patients = loadResult.Patients;
        _seriesList = _patients
            .SelectMany(patient => patient.Studies)
            .SelectMany(study => study.SeriesList)
            .ToList();

        _workspaceNote = loadResult.NoteText;
        _workspaceStatus = loadResult.StatusText;

        _activeSeriesIndex = 0;
        _activeSliceIndex = 0;
        _activeFrameIndex = 0;
        _toolMode = ViewerToolMode.Pan;
        _viewTransform = ViewTransform.Default;
        _activeWindowLevel = null;
        _measurementDraft = null;

        return BuildSnapshot();
    }

    public WorkspaceSnapshot SelectSeries(string seriesInstanceUid)
    {
        var index = _seriesList
            .Select((series, position) => new { series.SeriesInstanceUid, position })
            .FirstOrDefault(item => item.SeriesInstanceUid == seriesInstanceUid)?.position ?? 0;

        _activeSeriesIndex = index;
        _activeSliceIndex = 0;
        _activeFrameIndex = 0;
        _activeWindowLevel = null;
        _measurementDraft = null;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot MoveSlice(int delta)
    {
        var series = GetActiveSeries();
        if (series is null || series.Instances.Count == 0)
        {
            return BuildEmptySnapshot();
        }

        _activeSliceIndex = Math.Clamp(_activeSliceIndex + delta, 0, series.Instances.Count - 1);
        _activeFrameIndex = 0;
        _measurementDraft = null;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot MoveFrame(int delta)
    {
        var image = GetActiveImage();
        if (image is null)
        {
            return BuildEmptySnapshot();
        }

        var frameCount = Math.Max(image.FrameCount, 1);
        _activeFrameIndex = Math.Clamp(_activeFrameIndex + delta, 0, frameCount - 1);
        return BuildSnapshot();
    }

    public WorkspaceSnapshot SetTool(ViewerToolMode toolMode)
    {
        _toolMode = toolMode;
        if (!IsMeasurementTool(toolMode))
        {
            _measurementDraft = null;
        }

        return BuildSnapshot();
    }

    public WorkspaceSnapshot ResetView()
    {
        _viewTransform = ViewTransform.Default;
        _activeWindowLevel = null;
        _measurementDraft = null;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot Zoom(double factor)
    {
        var nextZoom = Math.Clamp(_viewTransform.Zoom * factor, 0.25, 8.0);
        _viewTransform = _viewTransform with { Zoom = nextZoom };
        return BuildSnapshot();
    }

    public WorkspaceSnapshot Rotate(double deltaDegrees)
    {
        var nextRotation = (_viewTransform.RotationDegrees + deltaDegrees) % 360.0;
        if (nextRotation < 0)
        {
            nextRotation += 360.0;
        }

        _viewTransform = _viewTransform with { RotationDegrees = nextRotation };
        return BuildSnapshot();
    }

    public WorkspaceSnapshot ToggleFlipHorizontal()
    {
        _viewTransform = _viewTransform with { FlipHorizontal = !_viewTransform.FlipHorizontal };
        return BuildSnapshot();
    }

    public WorkspaceSnapshot ToggleFlipVertical()
    {
        _viewTransform = _viewTransform with { FlipVertical = !_viewTransform.FlipVertical };
        return BuildSnapshot();
    }

    public WorkspaceSnapshot Pan(double deltaX, double deltaY)
    {
        _viewTransform = _viewTransform with
        {
            PanX = _viewTransform.PanX + deltaX,
            PanY = _viewTransform.PanY + deltaY,
        };

        return BuildSnapshot();
    }

    public WorkspaceSnapshot ApplyWindowLevelPreset(WindowLevel windowLevel)
    {
        _activeWindowLevel = windowLevel;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot AdjustWindowLevel(double widthDelta, double centerDelta)
    {
        var baseWindowLevel = GetEffectiveWindowLevel();
        var nextWidth = Math.Max(1.0, baseWindowLevel.Width + widthDelta);
        var nextCenter = baseWindowLevel.Center + centerDelta;
        _activeWindowLevel = new WindowLevel(nextWidth, nextCenter);
        return BuildSnapshot();
    }

    public WorkspaceSnapshot AddMeasurementPoint(Point2D point)
    {
        if (!IsMeasurementTool(_toolMode))
        {
            return BuildSnapshot();
        }

        var series = GetActiveSeries();
        var image = GetActiveImage(series);
        if (series is null || image is null)
        {
            return BuildEmptySnapshot();
        }

        var safePoint = ClampPoint(point, image);
        var requiredPointCount = GetRequiredMeasurementPointCount(_toolMode);

        if (_measurementDraft is null || _measurementDraft.SeriesInstanceUid != series.SeriesInstanceUid || _measurementDraft.ToolMode != _toolMode)
        {
            _measurementDraft = new MeasurementDraft(series.SeriesInstanceUid, _toolMode, new[] { safePoint }, safePoint);
            return BuildSnapshot();
        }

        var nextPoints = _measurementDraft.Points.Concat(new[] { safePoint }).Take(requiredPointCount).ToList();
        if (nextPoints.Count == requiredPointCount)
        {
            AddMeasurement(series.SeriesInstanceUid, CreateMeasurementAnnotation(_toolMode, nextPoints, image, isPreview: false));
            _measurementDraft = null;
        }
        else
        {
            _measurementDraft = _measurementDraft with { Points = nextPoints, PreviewPoint = safePoint };
        }

        return BuildSnapshot();
    }

    public WorkspaceSnapshot UpdateMeasurementPreview(Point2D point)
    {
        if (_measurementDraft is null)
        {
            return BuildSnapshot();
        }

        var image = GetActiveImage();
        if (image is null)
        {
            return BuildEmptySnapshot();
        }

        _measurementDraft = _measurementDraft with { PreviewPoint = ClampPoint(point, image) };
        return BuildSnapshot();
    }

    public WorkspaceSnapshot ClearMeasurements()
    {
        var series = GetActiveSeries();
        if (series is not null)
        {
            _measurementsBySeries.Remove(series.SeriesInstanceUid);
        }

        _measurementDraft = null;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot RemoveMeasurement(Guid measurementId)
    {
        var series = GetActiveSeries();
        if (series is not null && _measurementsBySeries.TryGetValue(series.SeriesInstanceUid, out var measurements))
        {
            measurements.RemoveAll(item => item.Id == measurementId);
            if (measurements.Count == 0)
            {
                _measurementsBySeries.Remove(series.SeriesInstanceUid);
            }
        }

        return BuildSnapshot();
    }

    private WorkspaceSnapshot BuildSnapshot()
    {
        var series = GetActiveSeries();
        if (series is null || series.Instances.Count == 0)
        {
            return BuildEmptySnapshot();
        }

        var patient = _patients.First();
        var study = patient.Studies.First();
        var image = series.Instances[_activeSliceIndex];
        var frameCount = Math.Max(image.FrameCount, 1);
        _activeFrameIndex = Math.Clamp(_activeFrameIndex, 0, frameCount - 1);
        var effectiveWindowLevel = GetEffectiveWindowLevel(image);
        var renderedViewport = _imageRenderService.Render(new RenderRequest(
            series,
            image,
            _activeSliceIndex,
            series.Instances.Count,
            _activeFrameIndex,
            frameCount,
            _toolMode,
            effectiveWindowLevel,
            _viewTransform));

        var viewportLoad = _viewportImageService.TryLoad(image.FilePath, _activeFrameIndex, effectiveWindowLevel);
        var owner = FindOwner(series.SeriesInstanceUid);
        if (owner is not null)
        {
            patient = owner.Value.Patient;
            study = owner.Value.Study;
        }

        var studyDateText = study.StudyDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        var measurementGuide = GetMeasurementGuideText();
        var notesText = viewportLoad.Succeeded
            ? $"{_workspaceNote} {viewportLoad.Message}{measurementGuide}".Trim()
            : $"{_workspaceNote} {viewportLoad.Message}{measurementGuide}".Trim();
        var placeholderText = viewportLoad.Succeeded
            ? renderedViewport.PlaceholderText
            : $"{renderedViewport.PlaceholderText}\n{viewportLoad.Message}";

        return new WorkspaceSnapshot(
            _seriesList.Select(seriesItem => new SeriesSummary(
                seriesItem.SeriesInstanceUid,
                $"{seriesItem.Modality} - {seriesItem.SeriesDescription}",
                seriesItem.Modality,
                seriesItem.Instances.Count)).ToList(),
            series.SeriesInstanceUid,
            viewportLoad.Image,
            _viewTransform,
            _toolMode,
            renderedViewport.Title,
            renderedViewport.Subtitle,
            placeholderText,
            renderedViewport.StatusText,
            patient.PatientName,
            $"{study.StudyDescription} {studyDateText}".Trim(),
            _toolMode.ToString(),
            effectiveWindowLevel.ToString(),
            $"Slice {_activeSliceIndex + 1} / {series.Instances.Count}",
            $"Frame {_activeFrameIndex + 1} / {frameCount}",
            frameCount,
            BuildViewText(),
            notesText,
            GetMeasurementsForSnapshot(series.SeriesInstanceUid, image));
    }

    private WindowLevel GetEffectiveWindowLevel(ImageInstance? image = null)
    {
        if (_activeWindowLevel is not null)
        {
            return _activeWindowLevel;
        }

        return image?.DefaultWindowLevel ?? new WindowLevel(255, 127.5);
    }

    private (Patient Patient, Study Study)? FindOwner(string seriesInstanceUid)
    {
        foreach (var patient in _patients)
        {
            foreach (var study in patient.Studies)
            {
                if (study.SeriesList.Any(series => series.SeriesInstanceUid == seriesInstanceUid))
                {
                    return (patient, study);
                }
            }
        }

        return null;
    }

    private Series? GetActiveSeries()
    {
        if (_seriesList.Count == 0)
        {
            return null;
        }

        return _seriesList[Math.Clamp(_activeSeriesIndex, 0, _seriesList.Count - 1)];
    }

    private ImageInstance? GetActiveImage(Series? series = null)
    {
        series ??= GetActiveSeries();
        if (series is null || series.Instances.Count == 0)
        {
            return null;
        }

        return series.Instances[Math.Clamp(_activeSliceIndex, 0, series.Instances.Count - 1)];
    }

    private IReadOnlyList<MeasurementAnnotation> GetMeasurementsForSnapshot(string seriesInstanceUid, ImageInstance image)
    {
        var measurements = new List<MeasurementAnnotation>();
        if (_measurementsBySeries.TryGetValue(seriesInstanceUid, out var storedMeasurements))
        {
            measurements.AddRange(storedMeasurements);
        }

        var previewMeasurement = BuildPreviewMeasurement(image);
        if (previewMeasurement is not null)
        {
            measurements.Add(previewMeasurement);
        }

        return measurements;
    }

    private MeasurementAnnotation? BuildPreviewMeasurement(ImageInstance image)
    {
        if (_measurementDraft is null || _measurementDraft.Points.Count == 0)
        {
            return null;
        }

        return _measurementDraft.ToolMode switch
        {
            ViewerToolMode.MeasureLength when _measurementDraft.Points.Count >= 1 => CreateMeasurementAnnotation(
                _measurementDraft.ToolMode,
                new[] { _measurementDraft.Points[0], _measurementDraft.PreviewPoint },
                image,
                isPreview: true),
            ViewerToolMode.MeasureAngle when _measurementDraft.Points.Count == 1 => CreateMeasurementAnnotation(
                _measurementDraft.ToolMode,
                new[] { _measurementDraft.Points[0], _measurementDraft.PreviewPoint },
                image,
                isPreview: true),
            ViewerToolMode.MeasureAngle when _measurementDraft.Points.Count >= 2 => CreateMeasurementAnnotation(
                _measurementDraft.ToolMode,
                new[] { _measurementDraft.Points[0], _measurementDraft.Points[1], _measurementDraft.PreviewPoint },
                image,
                isPreview: true),
            _ => null,
        };
    }

    private static MeasurementAnnotation CreateMeasurementAnnotation(
        ViewerToolMode toolMode,
        IReadOnlyList<Point2D> points,
        ImageInstance image,
        bool isPreview)
    {
        var label = toolMode switch
        {
            ViewerToolMode.MeasureLength when points.Count >= 2 => $"{CalculateLength(points[0], points[1], image.PixelSpacing):0.0} mm",
            ViewerToolMode.MeasureAngle when points.Count >= 3 => $"{CalculateAngle(points[0], points[1], points[2]):0.0}°",
            _ => string.Empty,
        };

        return new MeasurementAnnotation(Guid.NewGuid(), label, points.ToArray(), isPreview);
    }

    private static double CalculateLength(Point2D start, Point2D end, PixelSpacing pixelSpacing)
    {
        var deltaX = (end.X - start.X) * pixelSpacing.Column;
        var deltaY = (end.Y - start.Y) * pixelSpacing.Row;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static double CalculateAngle(Point2D first, Point2D vertex, Point2D third)
    {
        var vectorA = (X: first.X - vertex.X, Y: first.Y - vertex.Y);
        var vectorB = (X: third.X - vertex.X, Y: third.Y - vertex.Y);
        var magnitudeA = Math.Sqrt((vectorA.X * vectorA.X) + (vectorA.Y * vectorA.Y));
        var magnitudeB = Math.Sqrt((vectorB.X * vectorB.X) + (vectorB.Y * vectorB.Y));
        if (magnitudeA <= MinimumVectorMagnitude || magnitudeB <= MinimumVectorMagnitude)
        {
            return 0;
        }

        var cosine = ((vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y)) / (magnitudeA * magnitudeB);
        cosine = Math.Clamp(cosine, -1.0, 1.0);
        return Math.Acos(cosine) * 180.0 / Math.PI;
    }

    private static Point2D ClampPoint(Point2D point, ImageInstance image)
    {
        var maxX = Math.Max(0, image.Width - 1);
        var maxY = Math.Max(0, image.Height - 1);
        return new Point2D(
            Math.Clamp(point.X, 0, maxX),
            Math.Clamp(point.Y, 0, maxY));
    }

    private static bool IsMeasurementTool(ViewerToolMode toolMode)
    {
        return toolMode is ViewerToolMode.MeasureLength or ViewerToolMode.MeasureAngle;
    }

    private string BuildViewText()
    {
        var parts = new List<string>
        {
            $"Zoom {_viewTransform.Zoom:0.00}x",
            $"Pan ({_viewTransform.PanX:0},{_viewTransform.PanY:0})",
        };

        if (Math.Abs(_viewTransform.RotationDegrees) > double.Epsilon)
        {
            parts.Add($"Rot {_viewTransform.RotationDegrees:0}°");
        }

        if (_viewTransform.FlipHorizontal)
        {
            parts.Add("Flip H");
        }

        if (_viewTransform.FlipVertical)
        {
            parts.Add("Flip V");
        }

        return string.Join(" | ", parts);
    }

    private static int GetRequiredMeasurementPointCount(ViewerToolMode toolMode)
    {
        return toolMode == ViewerToolMode.MeasureAngle ? 3 : 2;
    }

    private void AddMeasurement(string seriesInstanceUid, MeasurementAnnotation measurement)
    {
        if (!_measurementsBySeries.TryGetValue(seriesInstanceUid, out var measurements))
        {
            measurements = new List<MeasurementAnnotation>();
            _measurementsBySeries[seriesInstanceUid] = measurements;
        }

        measurements.Add(measurement with { IsPreview = false });
    }

    private string GetMeasurementGuideText()
    {
        return _measurementDraft switch
        {
            { ToolMode: ViewerToolMode.MeasureLength, Points.Count: 1 } => " 长度测量：移动鼠标预览，单击第二个点完成。",
            { ToolMode: ViewerToolMode.MeasureAngle, Points.Count: 1 } => " 角度测量：单击顶点位置。",
            { ToolMode: ViewerToolMode.MeasureAngle, Points.Count: 2 } => " 角度测量：移动鼠标预览，单击第三个点完成。",
            _ => string.Empty,
        };
    }

    private WorkspaceSnapshot BuildEmptySnapshot()
    {
        return new WorkspaceSnapshot(
            Array.Empty<SeriesSummary>(),
            string.Empty,
            null,
            ViewTransform.Default,
            ViewerToolMode.None,
            "No series loaded",
            "Import DICOM studies to begin",
            "Waiting for study import",
            "Workspace is empty",
            "-",
            "-",
            ViewerToolMode.None.ToString(),
            "WW 0 / WL 0",
            "Slice 0 / 0",
            "Frame 0 / 0",
            0,
            "Zoom 1.00x | Pan (0,0)",
            _workspaceNote,
            Array.Empty<MeasurementAnnotation>());
    }

    private sealed record MeasurementDraft(
        string SeriesInstanceUid,
        ViewerToolMode ToolMode,
        IReadOnlyList<Point2D> Points,
        Point2D PreviewPoint);
}
