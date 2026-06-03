using System.Collections.ObjectModel;
using System.IO;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Wpf.Navigation;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace DicomViewer.Wpf.ViewModels;

/// <summary>
/// 曝光控制台页面的主 ViewModel。
/// 它负责接收界面输入、调用 <see cref="ExamWorkflowService"/> 推进检查流程，并把控制台快照同步回界面。
/// </summary>
public sealed class ExposureConsoleViewModel : BindableBase, INavigationAware
{
    private const int HistoryPageSize = 5;

    private readonly ExamWorkflowService _examWorkflowService;
    private readonly IRegionManager _regionManager;
    private bool _isApplyingConsoleSnapshot;
    private bool _isInitialized;
    private IReadOnlyList<ExamHistoryItem> _allHistoryItems = Array.Empty<ExamHistoryItem>();
    private WorklistItem? _selectedWorklistItem;
    private ExamHistoryItem? _selectedHistoryItem;
    private PacsRemoteStudy? _selectedRemoteStudy;
    private string _consoleStatusText = "控制台尚未初始化";
    private string _consoleNotesText = "请加载工作列表。";
    private string _consolePatientText = "未选择患者";
    private string _consoleOrderText = "未选择检查";
    private string _deviceStateText = "Idle";
    private string _workflowStatusText = "-";
    private string _lastExposureSummaryText = "暂无模拟曝光结果。";
    private string _lastArtifactPathText = "-";
    private string _pacsStatusText = "尚未发送到 PACS。";
    private string _pacsDetailsText = "-";
    private string _callingAeTitleText = PacsConfiguration.Default.CallingAeTitle;
    private string _calledAeTitleText = PacsConfiguration.Default.CalledAeTitle;
    private string _pacsHostText = PacsConfiguration.Default.Host;
    private string _pacsPortText = PacsConfiguration.Default.Port.ToString();
    private string _restApiPortText = PacsConfiguration.Default.RestApiPort.ToString();
    private string _outputDirectoryText = PacsConfiguration.Default.OutputDirectory;
    private string _localStoreHostText = PacsConfiguration.Default.LocalStoreHost;
    private string _localStorePortText = PacsConfiguration.Default.LocalStorePort.ToString();
    private string _remoteQueryPatientNameText = string.Empty;
    private string _remoteQueryPatientIdText = string.Empty;
    private string _remoteQueryStudyDescriptionText = string.Empty;
    private string _remoteQueryModalityText = string.Empty;
    private string _remoteQueryStudyDateFromText = string.Empty;
    private string _remoteQueryStudyDateToText = string.Empty;
    private string _kilovoltagePeakText = ExposureParameters.Default.KilovoltagePeak.ToString("0.#");
    private string _tubeCurrentMilliampereText = ExposureParameters.Default.TubeCurrentMilliampere.ToString("0.#");
    private string _exposureTimeMillisecondsText = ExposureParameters.Default.ExposureTimeMilliseconds.ToString("0.#");
    private string _milliampereSecondsText = ExposureParameters.Default.MilliampereSeconds.ToString("0.#");
    private string _sourceToImageDistanceText = ExposureParameters.Default.SourceToImageDistanceMillimeter.ToString("0.#");
    private string _minKilovoltagePeakText = ExposureParameterRange.Default.MinKilovoltagePeak.ToString("0.#");
    private string _maxKilovoltagePeakText = ExposureParameterRange.Default.MaxKilovoltagePeak.ToString("0.#");
    private string _minTubeCurrentMilliampereText = ExposureParameterRange.Default.MinTubeCurrentMilliampere.ToString("0.#");
    private string _maxTubeCurrentMilliampereText = ExposureParameterRange.Default.MaxTubeCurrentMilliampere.ToString("0.#");
    private string _minExposureTimeMillisecondsText = ExposureParameterRange.Default.MinExposureTimeMilliseconds.ToString("0.#");
    private string _maxExposureTimeMillisecondsText = ExposureParameterRange.Default.MaxExposureTimeMilliseconds.ToString("0.#");
    private string _minMilliampereSecondsText = ExposureParameterRange.Default.MinMilliampereSeconds.ToString("0.#");
    private string _maxMilliampereSecondsText = ExposureParameterRange.Default.MaxMilliampereSeconds.ToString("0.#");
    private string _minSourceToImageDistanceText = ExposureParameterRange.Default.MinSourceToImageDistanceMillimeter.ToString("0.#");
    private string _maxSourceToImageDistanceText = ExposureParameterRange.Default.MaxSourceToImageDistanceMillimeter.ToString("0.#");
    private string _bodyPartText = ExposureParameters.Default.BodyPart;
    private string _projectionText = ExposureParameters.Default.Projection;
    private bool _isAutomaticExposureControlEnabled = ExposureParameters.Default.IsAutomaticExposureControlEnabled;
    private bool _detectorConnected = true;
    private bool _tubeWarmedUp = true;
    private bool _doorClosed = true;
    private bool _pacsAvailable = true;
    private string _historyFilterText = string.Empty;
    private int _historyCurrentPage = 1;
    private int _historyTotalPages = 1;
    private int _historyFilteredCount;

    public ExposureConsoleViewModel(ExamWorkflowService examWorkflowService, IRegionManager regionManager)
    {
        _examWorkflowService = examWorkflowService;
        _regionManager = regionManager;
        WorklistItems = new ObservableCollection<WorklistItem>();
        HistoryItems = new ObservableCollection<ExamHistoryItem>();
        RemoteStudies = new ObservableCollection<PacsRemoteStudy>();
        InterlockMessages = new ObservableCollection<string>();
        AuditEntries = new ObservableCollection<string>();

        LoadWorklistCommand = new DelegateCommand(async () => await LoadWorklistAsync());
        RunInterlockCheckCommand = new DelegateCommand(RunInterlockCheck);
        ExecuteExposureCommand = new DelegateCommand(async () => await ExecuteExposureAsync()).ObservesCanExecute(() => CanExecuteExposure);
        SendToPacsCommand = new DelegateCommand(async () => await SendToPacsAsync()).ObservesCanExecute(() => CanSendToPacs);
        QueryPacsStudiesCommand = new DelegateCommand(async () => await QueryPacsStudiesAsync());
        QueryPacsStudiesViaDicomCommand = new DelegateCommand(async () => await QueryPacsStudiesViaDicomAsync());
        RetrieveRemoteStudyCommand = new DelegateCommand(async () => await RetrieveRemoteStudyAsync()).ObservesCanExecute(() => CanRetrieveRemoteStudy);
        RetrieveRemoteStudyViaDicomCommand = new DelegateCommand(async () => await RetrieveRemoteStudyViaDicomAsync()).ObservesCanExecute(() => CanRetrieveRemoteStudyViaDicom);
        ReviewExposureCommand = new DelegateCommand(ReviewExposure).ObservesCanExecute(() => CanReviewExposure);
        ReviewHistoryCommand = new DelegateCommand(ReviewHistory).ObservesCanExecute(() => CanReviewHistory);
        VerifyPacsConnectionCommand = new DelegateCommand(async () => await VerifyPacsConnectionAsync());
        ApplyExposureParametersCommand = new DelegateCommand(ApplyExposureParameters);
        ApplyPacsConfigurationCommand = new DelegateCommand(ApplyPacsConfiguration);
        PreviousHistoryPageCommand = new DelegateCommand(ChangeToPreviousHistoryPage).ObservesCanExecute(() => CanGoToPreviousHistoryPage);
        NextHistoryPageCommand = new DelegateCommand(ChangeToNextHistoryPage).ObservesCanExecute(() => CanGoToNextHistoryPage);
        ClearHistoryFilterCommand = new DelegateCommand(ClearHistoryFilter).ObservesCanExecute(() => CanClearHistoryFilter);
    }

    public ObservableCollection<WorklistItem> WorklistItems { get; }

    public ObservableCollection<ExamHistoryItem> HistoryItems { get; }

    public ObservableCollection<PacsRemoteStudy> RemoteStudies { get; }

    public ObservableCollection<string> InterlockMessages { get; }

    public ObservableCollection<string> AuditEntries { get; }

    public bool HasWorklistItems => WorklistItems.Count > 0;

    public bool HasInterlockFailures => InterlockMessages.Count > 0;

    public bool CanExecuteExposure => SelectedWorklistItem is not null && InterlockMessages.Count == 0 && DeviceStateText == DeviceOperationalState.Ready.ToString();

    public bool CanSendToPacs => LastArtifactPathText != "-";

    public bool CanReviewExposure => LastArtifactPathText != "-";

    public bool CanReviewHistory => SelectedHistoryItem is not null && !string.IsNullOrWhiteSpace(SelectedHistoryItem.ArtifactPath);

    public bool CanRetrieveRemoteStudy => SelectedRemoteStudy is not null;

    public bool CanRetrieveRemoteStudyViaDicom => SelectedRemoteStudy is not null && !string.IsNullOrWhiteSpace(SelectedRemoteStudy.StudyInstanceUid);

    public bool CanGoToPreviousHistoryPage => HistoryCurrentPage > 1;

    public bool CanGoToNextHistoryPage => HistoryCurrentPage < HistoryTotalPages;

    public bool CanClearHistoryFilter => !string.IsNullOrWhiteSpace(HistoryFilterText);

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

    public ExamHistoryItem? SelectedHistoryItem
    {
        get => _selectedHistoryItem;
        set
        {
            if (SetProperty(ref _selectedHistoryItem, value))
            {
                RaisePropertyChanged(nameof(CanReviewHistory));
            }
        }
    }

    public PacsRemoteStudy? SelectedRemoteStudy
    {
        get => _selectedRemoteStudy;
        set
        {
            if (SetProperty(ref _selectedRemoteStudy, value))
            {
                RaisePropertyChanged(nameof(CanRetrieveRemoteStudy));
                RaisePropertyChanged(nameof(CanRetrieveRemoteStudyViaDicom));
            }
        }
    }

    public string HistoryFilterText
    {
        get => _historyFilterText;
        set
        {
            if (!SetProperty(ref _historyFilterText, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(CanClearHistoryFilter));
            if (_isApplyingConsoleSnapshot)
            {
                return;
            }

            HistoryCurrentPage = 1;
            RefreshHistoryItems();
        }
    }

    public int HistoryCurrentPage
    {
        get => _historyCurrentPage;
        private set
        {
            if (SetProperty(ref _historyCurrentPage, value))
            {
                RaisePropertyChanged(nameof(HistoryPageText));
                RaisePropertyChanged(nameof(CanGoToPreviousHistoryPage));
                RaisePropertyChanged(nameof(CanGoToNextHistoryPage));
            }
        }
    }

    public int HistoryTotalPages
    {
        get => _historyTotalPages;
        private set
        {
            if (SetProperty(ref _historyTotalPages, value))
            {
                RaisePropertyChanged(nameof(HistoryPageText));
                RaisePropertyChanged(nameof(CanGoToPreviousHistoryPage));
                RaisePropertyChanged(nameof(CanGoToNextHistoryPage));
            }
        }
    }

    public string HistoryPageText => _historyFilteredCount == 0
        ? "暂无历史记录"
        : $"第 {HistoryCurrentPage} / {HistoryTotalPages} 页，共 {_historyFilteredCount} 条";

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
        private set
        {
            if (SetProperty(ref _lastArtifactPathText, value))
            {
                RaisePropertyChanged(nameof(CanSendToPacs));
            }
        }
    }

    public string PacsStatusText
    {
        get => _pacsStatusText;
        private set => SetProperty(ref _pacsStatusText, value);
    }

    public string PacsDetailsText
    {
        get => _pacsDetailsText;
        private set => SetProperty(ref _pacsDetailsText, value);
    }

    public string CallingAeTitleText
    {
        get => _callingAeTitleText;
        set => SetProperty(ref _callingAeTitleText, value);
    }

    public string CalledAeTitleText
    {
        get => _calledAeTitleText;
        set => SetProperty(ref _calledAeTitleText, value);
    }

    public string PacsHostText
    {
        get => _pacsHostText;
        set => SetProperty(ref _pacsHostText, value);
    }

    public string PacsPortText
    {
        get => _pacsPortText;
        set => SetProperty(ref _pacsPortText, value);
    }

    public string RestApiPortText
    {
        get => _restApiPortText;
        set => SetProperty(ref _restApiPortText, value);
    }

    public string OutputDirectoryText
    {
        get => _outputDirectoryText;
        set => SetProperty(ref _outputDirectoryText, value);
    }

    public string LocalStoreHostText
    {
        get => _localStoreHostText;
        set => SetProperty(ref _localStoreHostText, value);
    }

    public string LocalStorePortText
    {
        get => _localStorePortText;
        set => SetProperty(ref _localStorePortText, value);
    }

    public string RemoteQueryPatientNameText
    {
        get => _remoteQueryPatientNameText;
        set => SetProperty(ref _remoteQueryPatientNameText, value);
    }

    public string RemoteQueryPatientIdText
    {
        get => _remoteQueryPatientIdText;
        set => SetProperty(ref _remoteQueryPatientIdText, value);
    }

    public string RemoteQueryStudyDescriptionText
    {
        get => _remoteQueryStudyDescriptionText;
        set => SetProperty(ref _remoteQueryStudyDescriptionText, value);
    }

    public string RemoteQueryModalityText
    {
        get => _remoteQueryModalityText;
        set => SetProperty(ref _remoteQueryModalityText, value);
    }

    public string RemoteQueryStudyDateFromText
    {
        get => _remoteQueryStudyDateFromText;
        set => SetProperty(ref _remoteQueryStudyDateFromText, value);
    }

    public string RemoteQueryStudyDateToText
    {
        get => _remoteQueryStudyDateToText;
        set => SetProperty(ref _remoteQueryStudyDateToText, value);
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

    public string MinKilovoltagePeakText
    {
        get => _minKilovoltagePeakText;
        set => SetProperty(ref _minKilovoltagePeakText, value);
    }

    public string MaxKilovoltagePeakText
    {
        get => _maxKilovoltagePeakText;
        set => SetProperty(ref _maxKilovoltagePeakText, value);
    }

    public string MinTubeCurrentMilliampereText
    {
        get => _minTubeCurrentMilliampereText;
        set => SetProperty(ref _minTubeCurrentMilliampereText, value);
    }

    public string MaxTubeCurrentMilliampereText
    {
        get => _maxTubeCurrentMilliampereText;
        set => SetProperty(ref _maxTubeCurrentMilliampereText, value);
    }

    public string MinExposureTimeMillisecondsText
    {
        get => _minExposureTimeMillisecondsText;
        set => SetProperty(ref _minExposureTimeMillisecondsText, value);
    }

    public string MaxExposureTimeMillisecondsText
    {
        get => _maxExposureTimeMillisecondsText;
        set => SetProperty(ref _maxExposureTimeMillisecondsText, value);
    }

    public string MinMilliampereSecondsText
    {
        get => _minMilliampereSecondsText;
        set => SetProperty(ref _minMilliampereSecondsText, value);
    }

    public string MaxMilliampereSecondsText
    {
        get => _maxMilliampereSecondsText;
        set => SetProperty(ref _maxMilliampereSecondsText, value);
    }

    public string MinSourceToImageDistanceText
    {
        get => _minSourceToImageDistanceText;
        set => SetProperty(ref _minSourceToImageDistanceText, value);
    }

    public string MaxSourceToImageDistanceText
    {
        get => _maxSourceToImageDistanceText;
        set => SetProperty(ref _maxSourceToImageDistanceText, value);
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

    public DelegateCommand SendToPacsCommand { get; }

    public DelegateCommand QueryPacsStudiesCommand { get; }

    public DelegateCommand QueryPacsStudiesViaDicomCommand { get; }

    public DelegateCommand RetrieveRemoteStudyCommand { get; }

    public DelegateCommand RetrieveRemoteStudyViaDicomCommand { get; }

    public DelegateCommand ReviewExposureCommand { get; }

    public DelegateCommand ReviewHistoryCommand { get; }

    public DelegateCommand VerifyPacsConnectionCommand { get; }

    public DelegateCommand ApplyExposureParametersCommand { get; }

    public DelegateCommand ApplyPacsConfigurationCommand { get; }

    public DelegateCommand PreviousHistoryPageCommand { get; }

    public DelegateCommand NextHistoryPageCommand { get; }

    public DelegateCommand ClearHistoryFilterCommand { get; }

    /// <summary>
    /// Prism 导航始终复用当前控制台页面实例。
    /// </summary>
    public bool IsNavigationTarget(NavigationContext navigationContext)
    {
        return true;
    }

    /// <summary>
    /// 首次进入控制台页面时加载工作列表。
    /// </summary>
    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (!_isInitialized)
        {
            _ = InitializeAsync();
        }
    }

    /// <summary>
    /// 当前页面离开时无需额外清理。
    /// </summary>
    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
    }

    /// <summary>
    /// 执行控制台首屏初始化并拉取工作列表。
    /// </summary>
    private async Task InitializeAsync()
    {
        _isInitialized = true;
        ApplyConsoleSnapshot(await _examWorkflowService.LoadWorklistAsync());
    }

    /// <summary>
    /// 手动刷新工作列表。
    /// </summary>
    private async Task LoadWorklistAsync()
    {
        ApplyConsoleSnapshot(await _examWorkflowService.LoadWorklistAsync());
    }

    /// <summary>
    /// 先提交界面中的曝光参数，再执行一次模拟曝光。
    /// </summary>
    private async Task ExecuteExposureAsync()
    {
        ApplyExposureParameters();
        ApplyConsoleSnapshot(await _examWorkflowService.ExecuteExposureAsync());
    }

    /// <summary>
    /// 先提交 PACS 配置，再发送最近一次生成的 DICOM；发送成功后自动跳转回查看器。
    /// </summary>
    private async Task SendToPacsAsync()
    {
        ApplyPacsConfiguration();
        ApplyConsoleSnapshot(await _examWorkflowService.SendToPacsAsync());

        if (PacsStatusText.Contains("成功", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(LastArtifactPathText) && LastArtifactPathText != "-")
        {
            NavigateToViewer(LastArtifactPathText);
        }
    }

    /// <summary>
    /// 使用当前界面配置验证 PACS 连通性。
    /// </summary>
    private async Task VerifyPacsConnectionAsync()
    {
        ApplyPacsConfiguration();
        ApplyConsoleSnapshot(await _examWorkflowService.VerifyPacsConnectionAsync());
    }

    private async Task QueryPacsStudiesAsync()
    {
        ApplyPacsConfiguration();
        ApplyConsoleSnapshot(await _examWorkflowService.QueryPacsStudiesAsync(BuildPacsStudyQueryCriteria()));
    }

    private async Task QueryPacsStudiesViaDicomAsync()
    {
        ApplyPacsConfiguration();
        ApplyConsoleSnapshot(await _examWorkflowService.QueryPacsStudiesViaDicomAsync(BuildPacsStudyQueryCriteria()));
    }

    private async Task RetrieveRemoteStudyAsync()
    {
        var selectedRemoteStudy = SelectedRemoteStudy;
        if (selectedRemoteStudy is null)
        {
            return;
        }

        ApplyPacsConfiguration();
        var result = await _examWorkflowService.RetrievePacsStudyAsync(selectedRemoteStudy.RemoteStudyId);
        ApplyConsoleSnapshot(result.Snapshot);

        if (!string.IsNullOrWhiteSpace(result.ImportedDirectoryPath))
        {
            NavigateToViewerDirectory(result.ImportedDirectoryPath);
        }
    }

    private async Task RetrieveRemoteStudyViaDicomAsync()
    {
        var selectedRemoteStudy = SelectedRemoteStudy;
        if (selectedRemoteStudy is null || string.IsNullOrWhiteSpace(selectedRemoteStudy.StudyInstanceUid))
        {
            return;
        }

        ApplyPacsConfiguration();
        var result = await _examWorkflowService.RetrievePacsStudyViaDicomAsync(selectedRemoteStudy.StudyInstanceUid);
        ApplyConsoleSnapshot(result.Snapshot);

        if (!string.IsNullOrWhiteSpace(result.ImportedDirectoryPath))
        {
            NavigateToViewerDirectory(result.ImportedDirectoryPath);
        }
    }

    /// <summary>
    /// 在已有曝光结果时跳转到查看器页面进行回看。
    /// </summary>
    private void ReviewExposure()
    {
        if (string.IsNullOrWhiteSpace(LastArtifactPathText) || LastArtifactPathText == "-")
        {
            return;
        }

        NavigateToViewer(LastArtifactPathText);
    }

    private void ReviewHistory()
    {
        if (SelectedHistoryItem is null || string.IsNullOrWhiteSpace(SelectedHistoryItem.ArtifactPath))
        {
            return;
        }

        NavigateToViewer(SelectedHistoryItem.ArtifactPath);
    }

    private void ChangeToPreviousHistoryPage()
    {
        if (!CanGoToPreviousHistoryPage)
        {
            return;
        }

        HistoryCurrentPage--;
        RefreshHistoryItems();
    }

    private void ChangeToNextHistoryPage()
    {
        if (!CanGoToNextHistoryPage)
        {
            return;
        }

        HistoryCurrentPage++;
        RefreshHistoryItems();
    }

    private void ClearHistoryFilter()
    {
        if (!CanClearHistoryFilter)
        {
            return;
        }

        HistoryFilterText = string.Empty;
    }

    /// <summary>
    /// 把当前界面状态提交到服务层并执行联锁检查。
    /// </summary>
    private void RunInterlockCheck()
    {
        ApplyExposureParameters();
        ApplyConsoleSnapshot(_examWorkflowService.RunInterlockCheck());
    }

    /// <summary>
    /// 统一收集界面中的曝光参数、参数范围和设备状态，并同步到应用服务。
    /// </summary>
    private void ApplyExposureParameters()
    {
        var exposureParameters = BuildExposureParameters();
        var exposureParameterRange = BuildExposureParameterRange();
        ApplyConsoleSnapshot(_examWorkflowService.SetOperationalFlags(DetectorConnected, TubeWarmedUp, DoorClosed, PacsAvailable));
        ApplyConsoleSnapshot(_examWorkflowService.UpdateExposureParameterRange(exposureParameterRange));
        ApplyConsoleSnapshot(_examWorkflowService.UpdateExposureParameters(exposureParameters));
    }

    /// <summary>
    /// 把界面中的 PACS 连接信息同步到应用服务。
    /// </summary>
    private void ApplyPacsConfiguration()
    {
        ApplyConsoleSnapshot(_examWorkflowService.UpdatePacsConfiguration(BuildPacsConfiguration()));
    }

    /// <summary>
    /// 将文本输入框中的曝光参数解析为强类型值对象。
    /// </summary>
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

            /// <summary>
            /// 构造联锁检查用的曝光参数范围，并自动纠正最小值和最大值顺序。
            /// </summary>
    private ExposureParameterRange BuildExposureParameterRange()
    {
        var minKilovoltagePeak = ParseDoubleOrFallback(MinKilovoltagePeakText, ExposureParameterRange.Default.MinKilovoltagePeak);
        var maxKilovoltagePeak = ParseDoubleOrFallback(MaxKilovoltagePeakText, ExposureParameterRange.Default.MaxKilovoltagePeak);
        var minTubeCurrentMilliampere = ParseDoubleOrFallback(MinTubeCurrentMilliampereText, ExposureParameterRange.Default.MinTubeCurrentMilliampere);
        var maxTubeCurrentMilliampere = ParseDoubleOrFallback(MaxTubeCurrentMilliampereText, ExposureParameterRange.Default.MaxTubeCurrentMilliampere);
        var minExposureTimeMilliseconds = ParseDoubleOrFallback(MinExposureTimeMillisecondsText, ExposureParameterRange.Default.MinExposureTimeMilliseconds);
        var maxExposureTimeMilliseconds = ParseDoubleOrFallback(MaxExposureTimeMillisecondsText, ExposureParameterRange.Default.MaxExposureTimeMilliseconds);
        var minMilliampereSeconds = ParseDoubleOrFallback(MinMilliampereSecondsText, ExposureParameterRange.Default.MinMilliampereSeconds);
        var maxMilliampereSeconds = ParseDoubleOrFallback(MaxMilliampereSecondsText, ExposureParameterRange.Default.MaxMilliampereSeconds);
        var minSourceToImageDistance = ParseDoubleOrFallback(MinSourceToImageDistanceText, ExposureParameterRange.Default.MinSourceToImageDistanceMillimeter);
        var maxSourceToImageDistance = ParseDoubleOrFallback(MaxSourceToImageDistanceText, ExposureParameterRange.Default.MaxSourceToImageDistanceMillimeter);

        return new ExposureParameterRange(
            Math.Min(minKilovoltagePeak, maxKilovoltagePeak),
            Math.Max(minKilovoltagePeak, maxKilovoltagePeak),
            Math.Min(minTubeCurrentMilliampere, maxTubeCurrentMilliampere),
            Math.Max(minTubeCurrentMilliampere, maxTubeCurrentMilliampere),
            Math.Min(minExposureTimeMilliseconds, maxExposureTimeMilliseconds),
            Math.Max(minExposureTimeMilliseconds, maxExposureTimeMilliseconds),
            Math.Min(minMilliampereSeconds, maxMilliampereSeconds),
            Math.Max(minMilliampereSeconds, maxMilliampereSeconds),
            Math.Min(minSourceToImageDistance, maxSourceToImageDistance),
            Math.Max(minSourceToImageDistance, maxSourceToImageDistance));
    }

            /// <summary>
            /// 根据界面输入构造 PACS 配置对象。
            /// </summary>
    private PacsConfiguration BuildPacsConfiguration()
    {
        return new PacsConfiguration(
            string.IsNullOrWhiteSpace(CallingAeTitleText) ? PacsConfiguration.Default.CallingAeTitle : CallingAeTitleText.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(CalledAeTitleText) ? PacsConfiguration.Default.CalledAeTitle : CalledAeTitleText.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(PacsHostText) ? PacsConfiguration.Default.Host : PacsHostText.Trim(),
            int.TryParse(PacsPortText, out var port) ? port : PacsConfiguration.Default.Port,
            int.TryParse(RestApiPortText, out var restApiPort) ? restApiPort : PacsConfiguration.Default.RestApiPort,
            string.IsNullOrWhiteSpace(OutputDirectoryText) ? PacsConfiguration.Default.OutputDirectory : OutputDirectoryText.Trim(),
            string.IsNullOrWhiteSpace(LocalStoreHostText) ? PacsConfiguration.Default.LocalStoreHost : LocalStoreHostText.Trim(),
            int.TryParse(LocalStorePortText, out var localStorePort) ? localStorePort : PacsConfiguration.Default.LocalStorePort);
    }

    private PacsStudyQueryCriteria BuildPacsStudyQueryCriteria()
    {
        return new PacsStudyQueryCriteria(
            RemoteQueryPatientNameText.Trim(),
            RemoteQueryPatientIdText.Trim(),
            RemoteQueryStudyDescriptionText.Trim(),
            RemoteQueryModalityText.Trim().ToUpperInvariant(),
            ParseDateOrNull(RemoteQueryStudyDateFromText),
            ParseDateOrNull(RemoteQueryStudyDateToText));
    }

            /// <summary>
            /// 把应用服务返回的控制台快照投影到所有绑定属性、工作列表和审计日志。
            /// </summary>
    private void ApplyConsoleSnapshot(ConsoleSnapshot snapshot)
    {
        _isApplyingConsoleSnapshot = true;
        WorklistItems.Clear();
        foreach (var item in snapshot.WorklistItems)
        {
            WorklistItems.Add(item);
        }

        _allHistoryItems = snapshot.HistoryItems;

        RemoteStudies.Clear();
        foreach (var item in snapshot.RemoteStudies)
        {
            RemoteStudies.Add(item);
        }

        RaisePropertyChanged(nameof(HasWorklistItems));
        SelectedWorklistItem = snapshot.SelectedOrderId is null
            ? null
            : WorklistItems.FirstOrDefault(item => item.OrderId == snapshot.SelectedOrderId);
        RefreshHistoryItems();
        SelectedRemoteStudy = null;

        ConsoleStatusText = snapshot.StatusText;
        ConsoleNotesText = snapshot.NotesText;
        ConsolePatientText = snapshot.CurrentPatientText;
        ConsoleOrderText = snapshot.CurrentOrderText;
        DeviceStateText = snapshot.DeviceState.ToString();
        WorkflowStatusText = snapshot.WorkflowStatus?.ToString() ?? "-";
        LastExposureSummaryText = snapshot.LastExposureResult?.PreviewText ?? "暂无模拟曝光结果。";
        LastArtifactPathText = snapshot.LastExposureResult?.ArtifactPath ?? "-";
        PacsStatusText = snapshot.LastPacsStoreResult?.StatusText ?? "尚未发送到 PACS。";
        PacsDetailsText = snapshot.LastPacsStoreResult?.Details ?? "-";

        CallingAeTitleText = snapshot.PacsConfiguration.CallingAeTitle;
        CalledAeTitleText = snapshot.PacsConfiguration.CalledAeTitle;
        PacsHostText = snapshot.PacsConfiguration.Host;
        PacsPortText = snapshot.PacsConfiguration.Port.ToString();
        RestApiPortText = snapshot.PacsConfiguration.RestApiPort.ToString();
        OutputDirectoryText = snapshot.PacsConfiguration.OutputDirectory;
        LocalStoreHostText = snapshot.PacsConfiguration.LocalStoreHost;
        LocalStorePortText = snapshot.PacsConfiguration.LocalStorePort.ToString();

        KilovoltagePeakText = snapshot.ExposureParameters.KilovoltagePeak.ToString("0.#");
        TubeCurrentMilliampereText = snapshot.ExposureParameters.TubeCurrentMilliampere.ToString("0.#");
        ExposureTimeMillisecondsText = snapshot.ExposureParameters.ExposureTimeMilliseconds.ToString("0.#");
        MilliampereSecondsText = snapshot.ExposureParameters.MilliampereSeconds.ToString("0.#");
        SourceToImageDistanceText = snapshot.ExposureParameters.SourceToImageDistanceMillimeter.ToString("0.#");
        MinKilovoltagePeakText = snapshot.ExposureParameterRange.MinKilovoltagePeak.ToString("0.#");
        MaxKilovoltagePeakText = snapshot.ExposureParameterRange.MaxKilovoltagePeak.ToString("0.#");
        MinTubeCurrentMilliampereText = snapshot.ExposureParameterRange.MinTubeCurrentMilliampere.ToString("0.#");
        MaxTubeCurrentMilliampereText = snapshot.ExposureParameterRange.MaxTubeCurrentMilliampere.ToString("0.#");
        MinExposureTimeMillisecondsText = snapshot.ExposureParameterRange.MinExposureTimeMilliseconds.ToString("0.#");
        MaxExposureTimeMillisecondsText = snapshot.ExposureParameterRange.MaxExposureTimeMilliseconds.ToString("0.#");
        MinMilliampereSecondsText = snapshot.ExposureParameterRange.MinMilliampereSeconds.ToString("0.#");
        MaxMilliampereSecondsText = snapshot.ExposureParameterRange.MaxMilliampereSeconds.ToString("0.#");
        MinSourceToImageDistanceText = snapshot.ExposureParameterRange.MinSourceToImageDistanceMillimeter.ToString("0.#");
        MaxSourceToImageDistanceText = snapshot.ExposureParameterRange.MaxSourceToImageDistanceMillimeter.ToString("0.#");
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
        RaisePropertyChanged(nameof(CanSendToPacs));
        RaisePropertyChanged(nameof(CanReviewExposure));
        RaisePropertyChanged(nameof(CanReviewHistory));
        RaisePropertyChanged(nameof(CanRetrieveRemoteStudy));
        RaisePropertyChanged(nameof(CanRetrieveRemoteStudyViaDicom));
        RaisePropertyChanged(nameof(HistoryPageText));
        RaisePropertyChanged(nameof(CanGoToPreviousHistoryPage));
        RaisePropertyChanged(nameof(CanGoToNextHistoryPage));
    }

    private void RefreshHistoryItems()
    {
        var filteredItems = _allHistoryItems
            .Where(MatchesHistoryFilter)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        _historyFilteredCount = filteredItems.Count;
        HistoryTotalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(filteredItems.Count, 1) / (double)HistoryPageSize));
        HistoryCurrentPage = Math.Min(Math.Max(HistoryCurrentPage, 1), HistoryTotalPages);

        var pageItems = filteredItems
            .Skip((HistoryCurrentPage - 1) * HistoryPageSize)
            .Take(HistoryPageSize)
            .ToList();

        var selectedHistorySessionId = SelectedHistoryItem?.SessionId;
        HistoryItems.Clear();
        foreach (var item in pageItems)
        {
            HistoryItems.Add(item);
        }

        SelectedHistoryItem = selectedHistorySessionId is null
            ? null
            : HistoryItems.FirstOrDefault(item => item.SessionId == selectedHistorySessionId);
        RaisePropertyChanged(nameof(HistoryPageText));
    }

    private bool MatchesHistoryFilter(ExamHistoryItem item)
    {
        if (string.IsNullOrWhiteSpace(HistoryFilterText))
        {
            return true;
        }

        var keyword = HistoryFilterText.Trim();
        return item.PatientName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || item.ProcedureDescription.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || item.BodyPart.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || item.Projection.Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || item.WorkflowStatus.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
               || item.DeviceState.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 尝试把文本解析为双精度数，失败时回退到默认值。
    /// </summary>
    private static double ParseDoubleOrFallback(string text, double fallback)
    {
        return double.TryParse(text, out var value) ? value : fallback;
    }

    private static DateTime? ParseDateOrNull(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (DateTime.TryParse(trimmed, out var parsed))
        {
            return parsed.Date;
        }

        if (trimmed.Length == 8 && DateTime.TryParseExact(trimmed, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out parsed))
        {
            return parsed.Date;
        }

        return null;
    }

    /// <summary>
    /// 导航到查看器页面，并把生成文件所在目录作为导入目录传递过去。
    /// </summary>
    private void NavigateToViewer(string dicomFilePath)
    {
        var importPath = Path.GetDirectoryName(dicomFilePath);
        if (string.IsNullOrWhiteSpace(importPath))
        {
            return;
        }

        NavigateToViewerDirectory(importPath);
    }

    private void NavigateToViewerDirectory(string importPath)
    {
        var parameters = new NavigationParameters
        {
            { "importPath", importPath },
        };

        _regionManager.RequestNavigate(RegionNames.MainContentRegion, ViewNames.ViewerWorkspaceView, parameters);
    }
}