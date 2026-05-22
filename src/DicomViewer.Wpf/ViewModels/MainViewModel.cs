using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using Prism.Commands;
using Prism.Mvvm;

namespace DicomViewer.Wpf.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly WorkspaceService _workspaceService;
    private bool _isApplyingSnapshot;
    private SeriesSummary? _selectedSeries;
    private string _importPath = string.Empty;
    private BitmapSource? _viewportImageSource;
    private string _viewerTitle = "WPF Medical Viewer";
    private string _viewerSubtitle = "Workspace initializing";
    private string _placeholderText = "Preparing workspace";
    private string _statusText = "Starting";
    private string _patientText = "-";
    private string _studyText = "-";
    private string _toolText = ViewerToolMode.None.ToString();
    private string _windowText = "WW 0 / WL 0";
    private string _sliceText = "Slice 0 / 0";
    private string _frameText = "Frame 0 / 0";
    private string _viewText = "Zoom 1.00x | Pan (0,0)";
    private string _notesText = "Viewer shell not loaded yet.";
    private ViewerToolMode _currentToolMode = ViewerToolMode.None;
    private double _viewportZoom = 1.0;
    private double _viewportPanX;
    private double _viewportPanY;
    private double _imagePixelWidth;
    private double _imagePixelHeight;

    public MainViewModel(WorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
        SeriesItems = new ObservableCollection<SeriesSummary>();
        MeasurementItems = new ObservableCollection<MeasurementOverlayItem>();

        ImportFolderCommand = new DelegateCommand(async () => await ImportFolderAsync());
        PreviousSliceCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveSlice(-1))).ObservesCanExecute(() => HasSeriesItems);
        NextSliceCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveSlice(1))).ObservesCanExecute(() => HasSeriesItems);
        PreviousFrameCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveFrame(-1))).ObservesCanExecute(() => HasMultipleFrames);
        NextFrameCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveFrame(1))).ObservesCanExecute(() => HasMultipleFrames);
        PanToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.Pan)));
        WindowLevelToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.WindowLevel)));
        LengthToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureLength))).ObservesCanExecute(() => HasSeriesItems);
        AngleToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureAngle))).ObservesCanExecute(() => HasSeriesItems);
        ClearMeasurementsCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ClearMeasurements())).ObservesCanExecute(() => HasSeriesItems);
        ZoomInCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Zoom(1.2))).ObservesCanExecute(() => HasSeriesItems);
        ZoomOutCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Zoom(1.0 / 1.2))).ObservesCanExecute(() => HasSeriesItems);
        SoftTissuePresetCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(400, 40)))).ObservesCanExecute(() => HasSeriesItems);
        LungPresetCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(1500, -600)))).ObservesCanExecute(() => HasSeriesItems);
        BonePresetCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ApplyWindowLevelPreset(new(2000, 300)))).ObservesCanExecute(() => HasSeriesItems);
        IncreaseWindowCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(50, 0))).ObservesCanExecute(() => HasSeriesItems);
        DecreaseWindowCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(-50, 0))).ObservesCanExecute(() => HasSeriesItems);
        IncreaseLevelCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(0, 25))).ObservesCanExecute(() => HasSeriesItems);
        DecreaseLevelCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.AdjustWindowLevel(0, -25))).ObservesCanExecute(() => HasSeriesItems);
        ResetViewCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ResetView()));
    }

    public ObservableCollection<SeriesSummary> SeriesItems { get; }

    public ObservableCollection<MeasurementOverlayItem> MeasurementItems { get; }

    public bool HasSeriesItems => SeriesItems.Count > 0;

    public bool HasMultipleFrames => HasSeriesItems && !string.Equals(FrameText, "Frame 0 / 0", StringComparison.Ordinal) && !FrameText.EndsWith("/ 1", StringComparison.Ordinal);

    public string ImportPath
    {
        get => _importPath;
        set => SetProperty(ref _importPath, value);
    }

    public BitmapSource? ViewportImageSource
    {
        get => _viewportImageSource;
        private set
        {
            if (SetProperty(ref _viewportImageSource, value))
            {
                RaisePropertyChanged(nameof(ViewportImageVisibility));
                RaisePropertyChanged(nameof(PlaceholderVisibility));
            }
        }
    }

    public Visibility ViewportImageVisibility => ViewportImageSource is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PlaceholderVisibility => ViewportImageSource is null ? Visibility.Visible : Visibility.Collapsed;

    public SeriesSummary? SelectedSeries
    {
        get => _selectedSeries;
        set
        {
            if (!SetProperty(ref _selectedSeries, value) || value is null || _isApplyingSnapshot)
            {
                return;
            }

            ApplySnapshot(_workspaceService.SelectSeries(value.SeriesInstanceUid));
        }
    }

    public string ViewerTitle
    {
        get => _viewerTitle;
        private set => SetProperty(ref _viewerTitle, value);
    }

    public string ViewerSubtitle
    {
        get => _viewerSubtitle;
        private set => SetProperty(ref _viewerSubtitle, value);
    }

    public string PlaceholderText
    {
        get => _placeholderText;
        private set => SetProperty(ref _placeholderText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string PatientText
    {
        get => _patientText;
        private set => SetProperty(ref _patientText, value);
    }

    public string StudyText
    {
        get => _studyText;
        private set => SetProperty(ref _studyText, value);
    }

    public string ToolText
    {
        get => _toolText;
        private set => SetProperty(ref _toolText, value);
    }

    public string WindowText
    {
        get => _windowText;
        private set => SetProperty(ref _windowText, value);
    }

    public string SliceText
    {
        get => _sliceText;
        private set => SetProperty(ref _sliceText, value);
    }

    public string FrameText
    {
        get => _frameText;
        private set
        {
            if (SetProperty(ref _frameText, value))
            {
                RaisePropertyChanged(nameof(HasMultipleFrames));
            }
        }
    }

    public string ViewText
    {
        get => _viewText;
        private set => SetProperty(ref _viewText, value);
    }

    public string NotesText
    {
        get => _notesText;
        private set => SetProperty(ref _notesText, value);
    }

    public ViewerToolMode CurrentToolMode
    {
        get => _currentToolMode;
        private set => SetProperty(ref _currentToolMode, value);
    }

    public double ViewportZoom
    {
        get => _viewportZoom;
        private set => SetProperty(ref _viewportZoom, value);
    }

    public double ViewportPanX
    {
        get => _viewportPanX;
        private set => SetProperty(ref _viewportPanX, value);
    }

    public double ViewportPanY
    {
        get => _viewportPanY;
        private set => SetProperty(ref _viewportPanY, value);
    }

    public double ImagePixelWidth
    {
        get => _imagePixelWidth;
        private set => SetProperty(ref _imagePixelWidth, value);
    }

    public double ImagePixelHeight
    {
        get => _imagePixelHeight;
        private set => SetProperty(ref _imagePixelHeight, value);
    }

    public DelegateCommand PreviousSliceCommand { get; }

    public DelegateCommand ImportFolderCommand { get; }

    public DelegateCommand NextSliceCommand { get; }

    public DelegateCommand PreviousFrameCommand { get; }

    public DelegateCommand NextFrameCommand { get; }

    public DelegateCommand PanToolCommand { get; }

    public DelegateCommand WindowLevelToolCommand { get; }

    public DelegateCommand LengthToolCommand { get; }

    public DelegateCommand AngleToolCommand { get; }

    public DelegateCommand ClearMeasurementsCommand { get; }

    public DelegateCommand ZoomInCommand { get; }

    public DelegateCommand ZoomOutCommand { get; }

    public DelegateCommand SoftTissuePresetCommand { get; }

    public DelegateCommand LungPresetCommand { get; }

    public DelegateCommand BonePresetCommand { get; }

    public DelegateCommand IncreaseWindowCommand { get; }

    public DelegateCommand DecreaseWindowCommand { get; }

    public DelegateCommand IncreaseLevelCommand { get; }

    public DelegateCommand DecreaseLevelCommand { get; }

    public DelegateCommand ResetViewCommand { get; }

    public async Task InitializeAsync()
    {
        ApplySnapshot(await _workspaceService.LoadAsync());
    }

    public void ZoomFromWheel(double delta)
    {
        if (!HasSeriesItems)
        {
            return;
        }

        ApplySnapshot(_workspaceService.Zoom(delta >= 0 ? 1.1 : 1.0 / 1.1));
    }

    public void PanViewport(double deltaX, double deltaY)
    {
        if (!HasSeriesItems)
        {
            return;
        }

        ApplySnapshot(_workspaceService.Pan(deltaX, deltaY));
    }

    public void AdjustWindowLevelFromDrag(double deltaX, double deltaY)
    {
        if (!HasSeriesItems)
        {
            return;
        }

        ApplySnapshot(_workspaceService.AdjustWindowLevel(deltaX * 2.0, -deltaY * 2.0));
    }

    public void AddMeasurementPoint(Point imagePoint)
    {
        if (!HasSeriesItems)
        {
            return;
        }

        ApplySnapshot(_workspaceService.AddMeasurementPoint(new Point2D(imagePoint.X, imagePoint.Y)));
    }

    public void UpdateMeasurementPreview(Point imagePoint)
    {
        if (!HasSeriesItems)
        {
            return;
        }

        ApplySnapshot(_workspaceService.UpdateMeasurementPreview(new Point2D(imagePoint.X, imagePoint.Y)));
    }

    private async Task ImportFolderAsync()
    {
        ApplySnapshot(await _workspaceService.LoadAsync(ImportPath));
    }

    private void ApplySnapshot(WorkspaceSnapshot snapshot)
    {
        _isApplyingSnapshot = true;
        SeriesItems.Clear();
        foreach (var item in snapshot.SeriesItems)
        {
            SeriesItems.Add(item);
        }

        SelectedSeries = SeriesItems.FirstOrDefault(item => item.SeriesInstanceUid == snapshot.ActiveSeriesInstanceUid);
        _isApplyingSnapshot = false;
        RaisePropertyChanged(nameof(HasSeriesItems));
        RaisePropertyChanged(nameof(HasMultipleFrames));

        ViewerTitle = snapshot.ViewerTitle;
        ViewerSubtitle = snapshot.ViewerSubtitle;
        PlaceholderText = snapshot.PlaceholderText;
        ViewportImageSource = CreateBitmapSource(snapshot.ViewportImage);
        ImagePixelWidth = snapshot.ViewportImage?.Width ?? 0;
        ImagePixelHeight = snapshot.ViewportImage?.Height ?? 0;
        CurrentToolMode = snapshot.ToolMode;
        ViewportZoom = snapshot.ViewTransform.Zoom;
        ViewportPanX = snapshot.ViewTransform.PanX;
        ViewportPanY = snapshot.ViewTransform.PanY;
        StatusText = snapshot.StatusText;
        PatientText = snapshot.PatientText;
        StudyText = snapshot.StudyText;
        ToolText = snapshot.ToolText;
        WindowText = snapshot.WindowText;
        SliceText = snapshot.SliceText;
        FrameText = snapshot.FrameText;
        ViewText = snapshot.ViewText;
        NotesText = snapshot.NotesText;

        MeasurementItems.Clear();
        foreach (var measurement in snapshot.Measurements)
        {
            MeasurementItems.Add(ToOverlayItem(measurement));
        }
    }

    private static MeasurementOverlayItem ToOverlayItem(MeasurementAnnotation measurement)
    {
        var points = new PointCollection(measurement.Points.Select(point => new Point(point.X, point.Y)));
        var labelAnchor = measurement.Points.Count >= 3 ? measurement.Points[1] : measurement.Points.FirstOrDefault() ?? new Point2D(0, 0);
        if (measurement.Points.Count == 2)
        {
            labelAnchor = new Point2D(
                (measurement.Points[0].X + measurement.Points[1].X) / 2.0,
                (measurement.Points[0].Y + measurement.Points[1].Y) / 2.0);
        }

        return new MeasurementOverlayItem(
            points,
            labelAnchor.X + 6,
            labelAnchor.Y + 6,
            measurement.Label,
            measurement.IsPreview ? Brushes.Orange : Brushes.LimeGreen,
            measurement.IsPreview ? 1.5 : 2.0);
    }

    private static BitmapSource? CreateBitmapSource(ViewportImageData? image)
    {
        if (image is null || image.Width <= 0 || image.Height <= 0 || image.Pixels.Length == 0)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            image.Pixels,
            image.Stride);

        bitmap.Freeze();
        return bitmap;
    }
}
