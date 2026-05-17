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
    private readonly IStudyCatalogService _studyCatalogService;
    private readonly IImageRenderService _imageRenderService;
    private readonly IViewportImageService _viewportImageService;

    private IReadOnlyList<Patient> _patients = Array.Empty<Patient>();
    private IReadOnlyList<Series> _seriesList = Array.Empty<Series>();
    private int _activeSeriesIndex;
    private int _activeSliceIndex;
    private ViewerToolMode _toolMode = ViewerToolMode.Pan;
    private ViewTransform _viewTransform = ViewTransform.Default;
    private WindowLevel? _activeWindowLevel;
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
        // 这里统一负责“工作区重载”，上层 UI 不需要关心数据来自样例还是实际目录。
        _patients = await _studyCatalogService.LoadAsync(sourcePath, cancellationToken);
        _seriesList = _patients
            .SelectMany(patient => patient.Studies)
            .SelectMany(study => study.SeriesList)
            .ToList();

        _workspaceNote = string.IsNullOrWhiteSpace(sourcePath)
            ? "当前显示内置样例数据。"
            : _seriesList.Count == 0
                ? $"目录中未找到可解析的 DICOM 文件: {sourcePath}"
                : $"已从目录加载 {_seriesList.Count} 个序列: {sourcePath}";

        _workspaceStatus = string.IsNullOrWhiteSpace(sourcePath)
            ? "Sample workspace loaded"
            : _seriesList.Count == 0
                ? "No DICOM series found"
                : "DICOM metadata imported";

        _activeSeriesIndex = 0;
        _activeSliceIndex = 0;
        _toolMode = ViewerToolMode.Pan;
        _viewTransform = ViewTransform.Default;
        _activeWindowLevel = null;

        return BuildSnapshot();
    }

    public WorkspaceSnapshot SelectSeries(string seriesInstanceUid)
    {
        var index = _seriesList
            .Select((series, position) => new { series.SeriesInstanceUid, position })
            .FirstOrDefault(item => item.SeriesInstanceUid == seriesInstanceUid)?.position ?? 0;

        _activeSeriesIndex = index;
        _activeSliceIndex = 0;
        _activeWindowLevel = null;
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
        return BuildSnapshot();
    }

    public WorkspaceSnapshot SetTool(ViewerToolMode toolMode)
    {
        _toolMode = toolMode;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot ResetView()
    {
        // 重置时同时恢复缩放和窗宽窗位，便于回到该序列的默认显示状态。
        _viewTransform = ViewTransform.Default;
        _activeWindowLevel = null;
        return BuildSnapshot();
    }

    public WorkspaceSnapshot Zoom(double factor)
    {
        var nextZoom = Math.Clamp(_viewTransform.Zoom * factor, 0.25, 8.0);
        _viewTransform = _viewTransform with { Zoom = nextZoom };
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
        var effectiveWindowLevel = GetEffectiveWindowLevel(image);
        // 渲染层只负责生成当前视口描述；真正的像素缓冲由图像服务提供。
        var renderedViewport = _imageRenderService.Render(new RenderRequest(
            series,
            image,
            _activeSliceIndex,
            series.Instances.Count,
            _toolMode,
            effectiveWindowLevel,
            _viewTransform));

        // 当前版本仅加载单帧灰度图，后续可以在这里扩展多帧和彩色图像。
        var viewportImage = _viewportImageService.TryLoad(image.FilePath, 0, effectiveWindowLevel);
        var owner = FindOwner(series.SeriesInstanceUid);
        if (owner is not null)
        {
            patient = owner.Value.Patient;
            study = owner.Value.Study;
        }

        var studyDateText = study.StudyDate?.ToString("yyyy-MM-dd") ?? string.Empty;
        var notesText = viewportImage is null
            ? $"{_workspaceNote} 当前序列尚未生成位图，可能是样例数据、文件缺失或暂不支持的像素格式。"
            : $"{_workspaceNote} 当前视口已显示单帧灰度图像。";
        var placeholderText = viewportImage is null
            ? $"{renderedViewport.PlaceholderText}\nNo grayscale preview available"
            : renderedViewport.PlaceholderText;

        return new WorkspaceSnapshot(
            _seriesList.Select(seriesItem => new SeriesSummary(
                seriesItem.SeriesInstanceUid,
                $"{seriesItem.Modality} - {seriesItem.SeriesDescription}",
                seriesItem.Modality,
                seriesItem.Instances.Count)).ToList(),
            series.SeriesInstanceUid,
            viewportImage,
            renderedViewport.Title,
            renderedViewport.Subtitle,
            placeholderText,
            _workspaceStatus,
            patient.PatientName,
            $"{study.StudyDescription} {studyDateText}".Trim(),
            _toolMode.ToString(),
            effectiveWindowLevel.ToString(),
            $"Slice {_activeSliceIndex + 1} / {series.Instances.Count}",
            $"Zoom {_viewTransform.Zoom:0.00}x | Pan ({_viewTransform.PanX:0},{_viewTransform.PanY:0})",
            notesText
        );
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

    private WorkspaceSnapshot BuildEmptySnapshot()
    {
        return new WorkspaceSnapshot(
            Array.Empty<SeriesSummary>(),
            string.Empty,
            null,
            "No series loaded",
            "Import DICOM studies to begin",
            "Waiting for study import",
            "Workspace is empty",
            "-",
            "-",
            ViewerToolMode.None.ToString(),
            "WW 0 / WL 0",
            "Slice 0 / 0",
            "Zoom 1.00x | Pan (0,0)",
            _workspaceNote
        );
    }
}