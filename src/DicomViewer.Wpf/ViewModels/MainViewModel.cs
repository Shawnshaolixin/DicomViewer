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
    private readonly ExamWorkflowService _examWorkflowService;
    private bool _isApplyingSnapshot;
    private bool _isApplyingConsoleSnapshot;
    private SeriesSummary? _selectedSeries;
    private WorklistItem? _selectedWorklistItem;
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
    private int _frameCount;
    private string _viewText = "Zoom 1.00x | Pan (0,0)";
    private string _notesText = "Viewer shell not loaded yet.";
    private ViewerToolMode _currentToolMode = ViewerToolMode.None;
    private double _viewportZoom = 1.0;
    private double _viewportRotation;
    private double _viewportFlipScaleX = 1.0;
    private double _viewportFlipScaleY = 1.0;
    private double _viewportPanX;
    private double _viewportPanY;
    private double _imagePixelWidth;
    private double _imagePixelHeight;
    private MeasurementAnnotation? _selectedMeasurement;
    private string _consoleStatusText = "控制台尚未初始化";
    private string _consoleNotesText = "请加载工作列表。";
    private string _consolePatientText = "未选择患者";
    private string _consoleOrderText = "未选择检查";
    private string _deviceStateText = "Idle";
    private string _workflowStatusText = "-";
    private string _lastExposureSummaryText = "暂无模拟曝光结果。";
    private string _lastArtifactPathText = "-";
    private string _kilovoltagePeakText = ExposureParameters.Default.KilovoltagePeak.ToString("0.#");
    private string _tubeCurrentMilliampereText = ExposureParameters.Default.TubeCurrentMilliampere.ToString("0.#");
    private string _exposureTimeMillisecondsText = ExposureParameters.Default.ExposureTimeMilliseconds.ToString("0.#");
    private string _milliampereSecondsText = ExposureParameters.Default.MilliampereSeconds.ToString("0.#");
    private string _sourceToImageDistanceText = ExposureParameters.Default.SourceToImageDistanceMillimeter.ToString("0.#");
    private string _bodyPartText = ExposureParameters.Default.BodyPart;
    private string _projectionText = ExposureParameters.Default.Projection;
    private bool _isAutomaticExposureControlEnabled = ExposureParameters.Default.IsAutomaticExposureControlEnabled;
    private bool _detectorConnected = true;
    private bool _tubeWarmedUp = true;
    private bool _doorClosed = true;
    private bool _pacsAvailable = true;

    public MainViewModel(WorkspaceService workspaceService, ExamWorkflowService examWorkflowService)
    {
        _workspaceService = workspaceService;
        _examWorkflowService = examWorkflowService;
        SeriesItems = new ObservableCollection<SeriesSummary>();
        MeasurementItems = new ObservableCollection<MeasurementOverlayItem>();
        MeasurementListItems = new ObservableCollection<MeasurementAnnotation>();
        WorklistItems = new ObservableCollection<WorklistItem>();
        InterlockMessages = new ObservableCollection<string>();
        AuditEntries = new ObservableCollection<string>();

        ImportFolderCommand = new DelegateCommand(async () => await ImportFolderAsync());
        LoadWorklistCommand = new DelegateCommand(async () => await LoadWorklistAsync());
        RunInterlockCheckCommand = new DelegateCommand(RunInterlockCheck);
        ExecuteExposureCommand = new DelegateCommand(async () => await ExecuteExposureAsync()).ObservesCanExecute(() => CanExecuteExposure);
        ApplyExposureParametersCommand = new DelegateCommand(ApplyExposureParameters);
        PreviousSliceCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveSlice(-1))).ObservesCanExecute(() => HasSeriesItems);
        NextSliceCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveSlice(1))).ObservesCanExecute(() => HasSeriesItems);
        PreviousFrameCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveFrame(-1))).ObservesCanExecute(() => HasMultipleFrames);
        NextFrameCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.MoveFrame(1))).ObservesCanExecute(() => HasMultipleFrames);
        PanToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.Pan)));
        WindowLevelToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.WindowLevel)));
        LengthToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureLength))).ObservesCanExecute(() => HasSeriesItems);
        AngleToolCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.SetTool(ViewerToolMode.MeasureAngle))).ObservesCanExecute(() => HasSeriesItems);
        ClearMeasurementsCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ClearMeasurements())).ObservesCanExecute(() => HasSeriesItems);
        DeleteSelectedMeasurementCommand = new DelegateCommand(DeleteSelectedMeasurement).ObservesCanExecute(() => HasSelectedMeasurement);
        ZoomInCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Zoom(1.2))).ObservesCanExecute(() => HasSeriesItems);
        ZoomOutCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Zoom(1.0 / 1.2))).ObservesCanExecute(() => HasSeriesItems);
        RotateLeftCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Rotate(-90))).ObservesCanExecute(() => HasSeriesItems);
        RotateRightCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.Rotate(90))).ObservesCanExecute(() => HasSeriesItems);
        FlipHorizontalCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ToggleFlipHorizontal())).ObservesCanExecute(() => HasSeriesItems);
        FlipVerticalCommand = new DelegateCommand(() => ApplySnapshot(_workspaceService.ToggleFlipVertical())).ObservesCanExecute(() => HasSeriesItems);
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

    public ObservableCollection<MeasurementAnnotation> MeasurementListItems { get; }

    public ObservableCollection<WorklistItem> WorklistItems { get; }

    public ObservableCollection<string> InterlockMessages { get; }

    public ObservableCollection<string> AuditEntries { get; }

    public bool HasSeriesItems => SeriesItems.Count > 0;

    public bool HasMultipleFrames => HasSeriesItems && FrameCount > 1;

    public bool HasSelectedMeasurement => SelectedMeasurement is not null;

    public bool HasWorklistItems => WorklistItems.Count > 0;

    public bool HasInterlockFailures => InterlockMessages.Count > 0;

    public bool CanExecuteExposure => SelectedWorklistItem is not null && InterlockMessages.Count == 0 && DeviceStateText == DeviceOperationalState.Ready.ToString();

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

    public WorklistItem? SelectedWorklistItem
    {
        get => _selectedWorklistItem;
        set
        {
            if (!SetProperty(ref _selectedWorklistItem, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanExecuteExposure));
            if (!_isApplyingConsoleSnapshot && value is not null)
            {
                ApplyConsoleSnapshot(_examWorkflowService.SelectOrder(value.OrderId));
            }
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

    public int FrameCount
    {
        get => _frameCount;
        private set
        {
            if (SetProperty(ref _frameCount, value))
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

    public double ViewportRotation
    {
        get => _viewportRotation;
        private set => SetProperty(ref _viewportRotation, value);
    }

    public double ViewportFlipScaleX
    {
        get => _viewportFlipScaleX;
        private set => SetProperty(ref _viewportFlipScaleX, value);
    }

    public double ViewportFlipScaleY
    {
        get => _viewportFlipScaleY;
        private set => SetProperty(ref _viewportFlipScaleY, value);
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

    public double ImageCenterX => ImagePixelWidth / 2.0;

    public double ImageCenterY => ImagePixelHeight / 2.0;

    public MeasurementAnnotation? SelectedMeasurement
    {
        get => _selectedMeasurement;
        set
        {
            if (SetProperty(ref _selectedMeasurement, value))
            {
                RaisePropertyChanged(nameof(HasSelectedMeasurement));
            }
        }
    }

    public string ConsoleStatusText
    {
        get => _consoleStatusText;
        private set => SetProperty(ref _consoleStatusText, value);
    }

    public string ConsoleNotesText
    {
        get => _consoleNotesText;
        private set => SetProperty(ref _consoleNotesText, value);
    }

    public string ConsolePatientText
    {
        get => _consolePatientText;
        private set => SetProperty(ref _consolePatientText, value);
    }

    public string ConsoleOrderText
    {
        get => _consoleOrderText;
        private set => SetProperty(ref _consoleOrderText, value);
    }

    public string DeviceStateText
    {
        get => _deviceStateText;
        private set
        {
            if (SetProperty(ref _deviceStateText, value))
            {
                RaisePropertyChanged(nameof(DeviceWorkflowText));
                RaisePropertyChanged(nameof(CanExecuteExposure));
            }
        }
    }

    public string WorkflowStatusText
    {
        get => _workflowStatusText;
        private set
        {
            if (SetProperty(ref _workflowStatusText, value))
            {
                RaisePropertyChanged(nameof(DeviceWorkflowText));
            }
        }
    }

    public string DeviceWorkflowText => $"{DeviceStateText} / {WorkflowStatusText}";

    public string LastExposureSummaryText
    {
        get => _lastExposureSummaryText;
        private set => SetProperty(ref _lastExposureSummaryText, value);
    }

    public string LastArtifactPathText
    {
        get => _lastArtifactPathText;
        private set => SetProperty(ref _lastArtifactPathText, value);
    }

    public string KilovoltagePeakText
    {
        get => _kilovoltagePeakText;
        set => SetProperty(ref _kilovoltagePeakText, value);
    }

    public string TubeCurrentMilliampereText
    {
        get => _tubeCurrentMilliampereText;
        set => SetProperty(ref _tubeCurrentMilliampereText, value);
    }

    public string ExposureTimeMillisecondsText
    {
        get => _exposureTimeMillisecondsText;
        set => SetProperty(ref _exposureTimeMillisecondsText, value);
    }

    public string MilliampereSecondsText
    {
        get => _milliampereSecondsText;
        set => SetProperty(ref _milliampereSecondsText, value);
    }

    public string SourceToImageDistanceText
    {
        get => _sourceToImageDistanceText;
        set => SetProperty(ref _sourceToImageDistanceText, value);
    }

    public string BodyPartText
    {
        get => _bodyPartText;
        set => SetProperty(ref _bodyPartText, value);
    }

    public string ProjectionText
    {
        get => _projectionText;
        set => SetProperty(ref _projectionText, value);
    }

    public bool IsAutomaticExposureControlEnabled
    {
        get => _isAutomaticExposureControlEnabled;
        set => SetProperty(ref _isAutomaticExposureControlEnabled, value);
    }

    public bool DetectorConnected
    {
        get => _detectorConnected;
        set => SetProperty(ref _detectorConnected, value);
    }

    public bool TubeWarmedUp
    {
        get => _tubeWarmedUp;
        set => SetProperty(ref _tubeWarmedUp, value);
    }

    public bool DoorClosed
    {
        get => _doorClosed;
        set => SetProperty(ref _doorClosed, value);
    }

    public bool PacsAvailable
    {
        get => _pacsAvailable;
        set => SetProperty(ref _pacsAvailable, value);
    }

    public DelegateCommand PreviousSliceCommand { get; }

    public DelegateCommand ImportFolderCommand { get; }

    public DelegateCommand LoadWorklistCommand { get; }

    public DelegateCommand RunInterlockCheckCommand { get; }

    public DelegateCommand ExecuteExposureCommand { get; }

    public DelegateCommand ApplyExposureParametersCommand { get; }

    public DelegateCommand NextSliceCommand { get; }

    public DelegateCommand PreviousFrameCommand { get; }

    public DelegateCommand NextFrameCommand { get; }

    public DelegateCommand PanToolCommand { get; }

    public DelegateCommand WindowLevelToolCommand { get; }

    public DelegateCommand LengthToolCommand { get; }

    public DelegateCommand AngleToolCommand { get; }

    public DelegateCommand ClearMeasurementsCommand { get; }

    public DelegateCommand DeleteSelectedMeasurementCommand { get; }

    public DelegateCommand ZoomInCommand { get; }

    public DelegateCommand ZoomOutCommand { get; }

    public DelegateCommand RotateLeftCommand { get; }

    public DelegateCommand RotateRightCommand { get; }

    public DelegateCommand FlipHorizontalCommand { get; }

    public DelegateCommand FlipVerticalCommand { get; }

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
        ApplyConsoleSnapshot(await _examWorkflowService.LoadWorklistAsync());
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

    private async Task LoadWorklistAsync()
    {
        ApplyConsoleSnapshot(await _examWorkflowService.LoadWorklistAsync());
    }

    private async Task ExecuteExposureAsync()
    {
        ApplyExposureParameters();
        ApplyConsoleSnapshot(await _examWorkflowService.ExecuteExposureAsync());
    }

    private void RunInterlockCheck()
    {
        ApplyExposureParameters();
        ApplyConsoleSnapshot(_examWorkflowService.RunInterlockCheck());
    }

    private void ApplyExposureParameters()
    {
        var exposureParameters = BuildExposureParameters();
        ApplyConsoleSnapshot(_examWorkflowService.SetOperationalFlags(DetectorConnected, TubeWarmedUp, DoorClosed, PacsAvailable));
        ApplyConsoleSnapshot(_examWorkflowService.UpdateExposureParameters(exposureParameters));
    }

    private ExposureParameters BuildExposureParameters()
    {
        return new ExposureParameters(
            ParseDoubleOrFallback(KilovoltagePeakText, ExposureParameters.Default.KilovoltagePeak),
            ParseDoubleOrFallback(TubeCurrentMilliampereText, ExposureParameters.Default.TubeCurrentMilliampere),
            ParseDoubleOrFallback(ExposureTimeMillisecondsText, ExposureParameters.Default.ExposureTimeMilliseconds),
            ParseDoubleOrFallback(MilliampereSecondsText, ExposureParameters.Default.MilliampereSeconds),
            ParseDoubleOrFallback(SourceToImageDistanceText, ExposureParameters.Default.SourceToImageDistanceMillimeter),
            string.IsNullOrWhiteSpace(BodyPartText) ? ExposureParameters.Default.BodyPart : BodyPartText.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(ProjectionText) ? ExposureParameters.Default.Projection : ProjectionText.Trim().ToUpperInvariant(),
            IsAutomaticExposureControlEnabled);
    }

    private void ApplyConsoleSnapshot(ConsoleSnapshot snapshot)
    {
        _isApplyingConsoleSnapshot = true;
        WorklistItems.Clear();
        foreach (var item in snapshot.WorklistItems)
        {
            WorklistItems.Add(item);
        }

        RaisePropertyChanged(nameof(HasWorklistItems));
        SelectedWorklistItem = snapshot.SelectedOrderId is null
            ? null
            : WorklistItems.FirstOrDefault(item => item.OrderId == snapshot.SelectedOrderId);

        ConsoleStatusText = snapshot.StatusText;
        ConsoleNotesText = snapshot.NotesText;
        ConsolePatientText = snapshot.CurrentPatientText;
        ConsoleOrderText = snapshot.CurrentOrderText;
        DeviceStateText = snapshot.DeviceState.ToString();
        WorkflowStatusText = snapshot.WorkflowStatus?.ToString() ?? "-";
        LastExposureSummaryText = snapshot.LastExposureResult?.PreviewText ?? "暂无模拟曝光结果。";
        LastArtifactPathText = snapshot.LastExposureResult?.ArtifactPath ?? "-";

        KilovoltagePeakText = snapshot.ExposureParameters.KilovoltagePeak.ToString("0.#");
        TubeCurrentMilliampereText = snapshot.ExposureParameters.TubeCurrentMilliampere.ToString("0.#");
        ExposureTimeMillisecondsText = snapshot.ExposureParameters.ExposureTimeMilliseconds.ToString("0.#");
        MilliampereSecondsText = snapshot.ExposureParameters.MilliampereSeconds.ToString("0.#");
        SourceToImageDistanceText = snapshot.ExposureParameters.SourceToImageDistanceMillimeter.ToString("0.#");
        BodyPartText = snapshot.ExposureParameters.BodyPart;
        ProjectionText = snapshot.ExposureParameters.Projection;
        IsAutomaticExposureControlEnabled = snapshot.ExposureParameters.IsAutomaticExposureControlEnabled;

        InterlockMessages.Clear();
        foreach (var message in snapshot.InterlockMessages)
        {
            InterlockMessages.Add(message);
        }
        RaisePropertyChanged(nameof(HasInterlockFailures));

        AuditEntries.Clear();
        foreach (var entry in snapshot.AuditEntries.Reverse())
        {
            AuditEntries.Add(entry);
        }

        _isApplyingConsoleSnapshot = false;
        RaisePropertyChanged(nameof(CanExecuteExposure));
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
        RaisePropertyChanged(nameof(ImageCenterX));
        RaisePropertyChanged(nameof(ImageCenterY));
        CurrentToolMode = snapshot.ToolMode;
        ViewportZoom = snapshot.ViewTransform.Zoom;
        ViewportRotation = snapshot.ViewTransform.RotationDegrees;
        ViewportFlipScaleX = snapshot.ViewTransform.FlipHorizontal ? -1.0 : 1.0;
        ViewportFlipScaleY = snapshot.ViewTransform.FlipVertical ? -1.0 : 1.0;
        ViewportPanX = snapshot.ViewTransform.PanX;
        ViewportPanY = snapshot.ViewTransform.PanY;
        StatusText = snapshot.StatusText;
        PatientText = snapshot.PatientText;
        StudyText = snapshot.StudyText;
        ToolText = snapshot.ToolText;
        WindowText = snapshot.WindowText;
        SliceText = snapshot.SliceText;
        FrameText = snapshot.FrameText;
        FrameCount = snapshot.FrameCount;
        ViewText = snapshot.ViewText;
        NotesText = snapshot.NotesText;

        var selectedMeasurementId = SelectedMeasurement?.Id;
        MeasurementItems.Clear();
        foreach (var measurement in snapshot.Measurements)
        {
            MeasurementItems.Add(ToOverlayItem(measurement));
        }

        MeasurementListItems.Clear();
        foreach (var measurement in snapshot.Measurements.Where(item => !item.IsPreview))
        {
            MeasurementListItems.Add(measurement);
        }

        SelectedMeasurement = selectedMeasurementId is null
            ? null
            : MeasurementListItems.FirstOrDefault(item => item.Id == selectedMeasurementId.Value);
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
            measurement.Id,
            points,
            labelAnchor.X + 6,
            labelAnchor.Y + 6,
            measurement.Label,
            measurement.IsPreview ? Brushes.Orange : Brushes.LimeGreen,
            measurement.IsPreview ? 1.5 : 2.0);
    }

    private void DeleteSelectedMeasurement()
    {
        if (SelectedMeasurement is null)
        {
            return;
        }

        ApplySnapshot(_workspaceService.RemoveMeasurement(SelectedMeasurement.Id));
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

    private static double ParseDoubleOrFallback(string text, double fallback)
    {
        return double.TryParse(text, out var value) ? value : fallback;
    }
}
