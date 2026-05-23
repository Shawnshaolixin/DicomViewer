using System.Collections.ObjectModel;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace DicomViewer.Wpf.ViewModels;

public sealed class ExposureConsoleViewModel : BindableBase, INavigationAware
{
    private readonly ExamWorkflowService _examWorkflowService;
    private bool _isApplyingConsoleSnapshot;
    private bool _isInitialized;
    private WorklistItem? _selectedWorklistItem;
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

    public ExposureConsoleViewModel(ExamWorkflowService examWorkflowService)
    {
        _examWorkflowService = examWorkflowService;
        WorklistItems = new ObservableCollection<WorklistItem>();
        InterlockMessages = new ObservableCollection<string>();
        AuditEntries = new ObservableCollection<string>();

        LoadWorklistCommand = new DelegateCommand(async () => await LoadWorklistAsync());
        RunInterlockCheckCommand = new DelegateCommand(RunInterlockCheck);
        ExecuteExposureCommand = new DelegateCommand(async () => await ExecuteExposureAsync()).ObservesCanExecute(() => CanExecuteExposure);
        ApplyExposureParametersCommand = new DelegateCommand(ApplyExposureParameters);
    }

    public ObservableCollection<WorklistItem> WorklistItems { get; }

    public ObservableCollection<string> InterlockMessages { get; }

    public ObservableCollection<string> AuditEntries { get; }

    public bool HasWorklistItems => WorklistItems.Count > 0;

    public bool HasInterlockFailures => InterlockMessages.Count > 0;

    public bool CanExecuteExposure => SelectedWorklistItem is not null && InterlockMessages.Count == 0 && DeviceStateText == DeviceOperationalState.Ready.ToString();

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

    public DelegateCommand LoadWorklistCommand { get; }

    public DelegateCommand RunInterlockCheckCommand { get; }

    public DelegateCommand ExecuteExposureCommand { get; }

    public DelegateCommand ApplyExposureParametersCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (!_isInitialized)
        {
            _ = InitializeAsync();
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    private async Task InitializeAsync()
    {
        _isInitialized = true;
        ApplyConsoleSnapshot(await _examWorkflowService.LoadWorklistAsync());
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

    private static double ParseDoubleOrFallback(string text, double fallback)
    {
        return double.TryParse(text, out var value) ? value : fallback;
    }
}