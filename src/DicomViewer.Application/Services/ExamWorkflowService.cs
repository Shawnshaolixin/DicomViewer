using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Services;

/// <summary>
/// 管理曝光控制台的检查流程状态。
/// 该服务串联工作列表、联锁检查、模拟曝光、PACS 发送和审计记录，并把过程状态汇总为 <see cref="ConsoleSnapshot"/>。
/// </summary>
public sealed class ExamWorkflowService
{
    private readonly IWorklistService _worklistService;
    private readonly IInterlockService _interlockService;
    private readonly IExposureSimulationService _exposureSimulationService;
    private readonly IPacsStoreService _pacsStoreService;
    private readonly IAuditService _auditService;
    private readonly IConsoleConfigurationStore _consoleConfigurationStore;
    private readonly IExamSessionStore _examSessionStore;
    private readonly IPacsSendRecordStore _pacsSendRecordStore;
    private const int HistoryItemLimit = 12;

    private IReadOnlyList<ImagingOrder> _orders = Array.Empty<ImagingOrder>();
    private ImagingOrder? _selectedOrder;
    private ExamSession? _session;
    private ExposureParameters _exposureParameters = ExposureParameters.Default;
    private ExposureParameterRange _exposureParameterRange = ExposureParameterRange.Default;
    private InterlockCheckResult _lastInterlockResult = InterlockCheckResult.Fail((InterlockCode.NoActiveOrder, "未选择检查任务。"));
    private ExposureResult? _lastExposureResult;
    private PacsStoreResult? _lastPacsStoreResult;
    private IReadOnlyList<PacsRemoteStudy> _remoteStudies = Array.Empty<PacsRemoteStudy>();
    private PacsConfiguration _pacsConfiguration = PacsConfiguration.Default;
    private string _statusText = "控制台尚未初始化";
    private string _notesText = "请先加载工作列表。";
    private bool _isConfigurationLoaded;

    public ExamWorkflowService(
        IWorklistService worklistService,
        IInterlockService interlockService,
        IExposureSimulationService exposureSimulationService,
        IPacsStoreService pacsStoreService,
        IAuditService auditService,
        IConsoleConfigurationStore consoleConfigurationStore,
        IExamSessionStore examSessionStore,
        IPacsSendRecordStore pacsSendRecordStore)
    {
        _worklistService = worklistService;
        _interlockService = interlockService;
        _exposureSimulationService = exposureSimulationService;
        _pacsStoreService = pacsStoreService;
        _auditService = auditService;
        _consoleConfigurationStore = consoleConfigurationStore;
        _examSessionStore = examSessionStore;
        _pacsSendRecordStore = pacsSendRecordStore;
    }

    /// <summary>
    /// 从工作列表服务加载待检查任务。
    /// </summary>
    public bool DetectorConnected { get; private set; } = true;

    public bool TubeWarmedUp { get; private set; } = true;

    public bool DoorClosed { get; private set; } = true;

    public bool PacsAvailable { get; private set; } = true;

    /// <summary>
    /// 从工作列表服务加载待检查任务。
    /// </summary>
    public async Task<ConsoleSnapshot> LoadWorklistAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigurationLoaded();
        _orders = await _worklistService.QueryAsync(MwlQueryCriteria.Empty, cancellationToken);
        _statusText = _orders.Count == 0 ? "工作列表为空" : $"已加载 {_orders.Count} 条工作列表";
        _notesText = _orders.Count == 0 ? "请检查模拟数据源。" : "请选择一条检查任务进入准备状态。";
        _auditService.Record(_statusText);
        return BuildSnapshot();
    }

    /// <summary>
    /// 选择工作列表中的一条检查任务，并为其创建检查会话。
    /// </summary>
    public ConsoleSnapshot SelectOrder(string orderId)
    {
        _selectedOrder = _orders.FirstOrDefault(order => order.OrderId == orderId);
        _lastExposureResult = null;
        _lastPacsStoreResult = null;

        if (_selectedOrder is null)
        {
            _session = null;
            _statusText = "未找到检查任务";
            _notesText = $"工作列表中不存在任务: {orderId}";
            _lastInterlockResult = InterlockCheckResult.Fail((InterlockCode.NoActiveOrder, "未选择检查任务。"));
            _auditService.Record(_statusText);
            return BuildSnapshot();
        }

        _exposureParameters = _exposureParameters with
        {
            BodyPart = _selectedOrder.BodyPart,
            Projection = _selectedOrder.Projection,
        };

        _session = new ExamSession(
            Guid.NewGuid().ToString("N"),
            _selectedOrder,
            _exposureParameters,
            ExamWorkflowStatus.InProgress,
            DeviceOperationalState.Preparing,
            DateTime.UtcNow,
            null,
            null,
            null,
            MppsStatus.None,
            null,
            null,
            null,
            string.IsNullOrWhiteSpace(_selectedOrder.ScheduledProcedureStepId) ? null : _selectedOrder.ScheduledProcedureStepId,
            string.IsNullOrWhiteSpace(_selectedOrder.AccessionNumber) ? null : _selectedOrder.AccessionNumber);

        _statusText = "已创建检查会话";
        _notesText = $"当前任务: {_selectedOrder.PatientName} / {_selectedOrder.ProcedureDescription}";
        PersistSession();
        _auditService.Record($"选择检查任务: {_selectedOrder.OrderId}");
        return RunInterlockCheck();
    }

    /// <summary>
    /// 更新当前会话使用的曝光参数。
    /// </summary>
    public ConsoleSnapshot UpdateExposureParameters(ExposureParameters exposureParameters)
    {
        _exposureParameters = exposureParameters;

        if (_session is not null)
        {
            _session = _session with { ExposureParameters = exposureParameters };
        }

        _statusText = "曝光参数已更新";
        _notesText = $"kV={exposureParameters.KilovoltagePeak:0.#}, mA={exposureParameters.TubeCurrentMilliampere:0.#}, ms={exposureParameters.ExposureTimeMilliseconds:0.#}";
        _auditService.Record("修改曝光参数");
        return BuildSnapshot();
    }

    /// <summary>
    /// 更新 PACS 连接配置。
    /// </summary>
    public ConsoleSnapshot UpdatePacsConfiguration(PacsConfiguration pacsConfiguration)
    {
        _pacsConfiguration = pacsConfiguration;
        SaveConfiguration();
        _statusText = "PACS 配置已更新";
        _notesText = $"DICOM {pacsConfiguration.Host}:{pacsConfiguration.Port} / REST {pacsConfiguration.Host}:{pacsConfiguration.RestApiPort}";
        _auditService.Record("修改 PACS 配置");
        return BuildSnapshot();
    }

    /// <summary>
    /// 更新联锁检查使用的参数范围。
    /// </summary>
    public ConsoleSnapshot UpdateExposureParameterRange(ExposureParameterRange exposureParameterRange)
    {
        _exposureParameterRange = exposureParameterRange;
        SaveConfiguration();
        _statusText = "参数范围已更新";
        _notesText = $"kV={exposureParameterRange.MinKilovoltagePeak:0.#}-{exposureParameterRange.MaxKilovoltagePeak:0.#}, mA={exposureParameterRange.MinTubeCurrentMilliampere:0.#}-{exposureParameterRange.MaxTubeCurrentMilliampere:0.#}";
        _auditService.Record("修改参数范围");
        return BuildSnapshot();
    }

    private void EnsureConfigurationLoaded()
    {
        if (_isConfigurationLoaded)
        {
            return;
        }

        var configuration = _consoleConfigurationStore.Load();
        _pacsConfiguration = configuration.PacsConfiguration;
        _exposureParameterRange = configuration.ExposureParameterRange;
        _isConfigurationLoaded = true;
    }

    private void SaveConfiguration()
    {
        _isConfigurationLoaded = true;
        _consoleConfigurationStore.Save(new ConsoleConfiguration(_pacsConfiguration, _exposureParameterRange));
    }

    /// <summary>
    /// 更新设备联锁相关的运行标志，例如探测器、机房门和 PACS 可用性。
    /// </summary>
    public ConsoleSnapshot SetOperationalFlags(bool detectorConnected, bool tubeWarmedUp, bool doorClosed, bool pacsAvailable)
    {
        DetectorConnected = detectorConnected;
        TubeWarmedUp = tubeWarmedUp;
        DoorClosed = doorClosed;
        PacsAvailable = pacsAvailable;

        _statusText = "设备联锁状态已更新";
        _notesText = $"Detector={(detectorConnected ? "On" : "Off")}, Tube={(tubeWarmedUp ? "Warm" : "Cold")}, Door={(doorClosed ? "Closed" : "Open")}, PACS={(pacsAvailable ? "Up" : "Down")}";
        return BuildSnapshot();
    }

    /// <summary>
    /// 执行联锁检查，并根据结果把设备状态推进到 Ready 或回退到 Preparing。
    /// </summary>
    public ConsoleSnapshot RunInterlockCheck()
    {
        var deviceState = _session?.DeviceState ?? DeviceOperationalState.Idle;
        _lastInterlockResult = _interlockService.Evaluate(
            _selectedOrder,
            _exposureParameters,
            _exposureParameterRange,
            deviceState,
            DetectorConnected,
            TubeWarmedUp,
            DoorClosed,
            PacsAvailable);

        if (_lastInterlockResult.IsPassed)
        {
            if (_session is not null)
            {
                _session = _session with
                {
                    DeviceState = DeviceOperationalState.Ready,
                    WorkflowStatus = ExamWorkflowStatus.Ready,
                    ExposureParameters = _exposureParameters,
                };
            }

            _statusText = "联锁检查通过";
            _notesText = "设备已就绪，可以执行模拟曝光。";
            _auditService.Record("联锁检查通过");
        }
        else
        {
            if (_session is not null)
            {
                _session = _session with
                {
                    DeviceState = DeviceOperationalState.Preparing,
                    WorkflowStatus = ExamWorkflowStatus.InProgress,
                    ExposureParameters = _exposureParameters,
                };
            }

            _statusText = "联锁检查失败";
            _notesText = string.Join("；", _lastInterlockResult.Messages);
            _auditService.Record($"联锁检查失败: {_notesText}");
        }

        PersistSession();

        return BuildSnapshot();
    }

    /// <summary>
    /// 执行一次模拟曝光，生成 DICOM 文件并刷新当前会话状态。
    /// </summary>
    public async Task<ConsoleSnapshot> ExecuteExposureAsync(CancellationToken cancellationToken = default)
    {
        _ = RunInterlockCheck();
        if (!_lastInterlockResult.IsPassed || _session is null)
        {
            return BuildSnapshot();
        }

        _session = _session with
        {
            DeviceState = DeviceOperationalState.Exposing,
            WorkflowStatus = ExamWorkflowStatus.Acquiring,
            ExposureParameters = _exposureParameters,
        };
        _statusText = "正在执行模拟曝光";
        _notesText = "已进入 Exposing 状态。";
        _auditService.Record("曝光执行开始");

        _session = _session with
        {
            DeviceState = DeviceOperationalState.Processing,
            WorkflowStatus = ExamWorkflowStatus.Processing,
        };

        _lastExposureResult = await _exposureSimulationService.RunAsync(_session, _pacsConfiguration.OutputDirectory, cancellationToken);
        _auditService.Record($"DICOM 生成: {_lastExposureResult.ArtifactPath}");

        _session = _session with
        {
            DeviceState = DeviceOperationalState.Ready,
            WorkflowStatus = ExamWorkflowStatus.Ready,
            LastExposureAtUtc = _lastExposureResult.AcquiredAtUtc,
            LastGeneratedArtifact = _lastExposureResult.ArtifactPath,
        };

        PersistSession();

        _statusText = "模拟曝光完成";
        _notesText = _lastExposureResult.PreviewText;
        _auditService.Record("曝光执行完成");

        return BuildSnapshot();
    }

    /// <summary>
    /// 将最近一次曝光生成的 DICOM 文件发送到 PACS。
    /// </summary>
    public async Task<ConsoleSnapshot> SendToPacsAsync(CancellationToken cancellationToken = default)
    {
        if (_lastExposureResult is null)
        {
            _statusText = "PACS 发送失败";
            _notesText = "当前没有可发送的 DICOM 文件，请先执行曝光。";
            _auditService.Record("PACS 发送失败: 无可发送文件");
            return BuildSnapshot();
        }

        _statusText = "正在发送到 PACS";
        _notesText = $"发送 {_lastExposureResult.ArtifactPath} 到 {_pacsConfiguration.Host}:{_pacsConfiguration.Port}";
        _auditService.Record("PACS 发送开始");

        if (_session is not null)
        {
            _session = _session with
            {
                WorkflowStatus = ExamWorkflowStatus.Sending,
                DeviceState = DeviceOperationalState.Processing,
            };
        }

        _lastPacsStoreResult = await _pacsStoreService.SendAsync(_lastExposureResult.ArtifactPath, _pacsConfiguration, cancellationToken);

        if (_session is not null)
        {
            _session = _session with
            {
                WorkflowStatus = _lastPacsStoreResult.IsSuccess ? ExamWorkflowStatus.Completed : ExamWorkflowStatus.Failed,
                DeviceState = _lastPacsStoreResult.IsSuccess ? DeviceOperationalState.Ready : DeviceOperationalState.Error,
            };

            PersistSession();
        }

        if (_session is not null)
        {
            _pacsSendRecordStore.Add(new PacsSendRecord(
                _session.SessionId,
                _lastPacsStoreResult.FilePath,
                _lastPacsStoreResult.IsSuccess,
                _lastPacsStoreResult.StatusText,
                _lastPacsStoreResult.Details,
                _lastPacsStoreResult.CalledAeTitle,
                _lastPacsStoreResult.Host,
                _lastPacsStoreResult.Port,
                _lastPacsStoreResult.ProcessedAtUtc));
        }

        _statusText = _lastPacsStoreResult.StatusText;
        _notesText = _lastPacsStoreResult.Details;
        _auditService.Record($"PACS 发送{(_lastPacsStoreResult.IsSuccess ? "成功" : "失败")}: {_lastPacsStoreResult.Details}");
        return BuildSnapshot();
    }

    /// <summary>
    /// 使用 C-ECHO 验证当前 PACS 配置是否可达。
    /// </summary>
    public async Task<ConsoleSnapshot> VerifyPacsConnectionAsync(CancellationToken cancellationToken = default)
    {
        _statusText = "正在验证 PACS 连通性";
        _notesText = $"使用 C-ECHO 验证 {_pacsConfiguration.Host}:{_pacsConfiguration.Port}";
        _auditService.Record("PACS 连通性验证开始");

        _lastPacsStoreResult = await _pacsStoreService.VerifyConnectionAsync(_pacsConfiguration, cancellationToken);
        _statusText = _lastPacsStoreResult.StatusText;
        _notesText = _lastPacsStoreResult.Details;
        _auditService.Record($"PACS 连通性验证{(_lastPacsStoreResult.IsSuccess ? "成功" : "失败")}: {_lastPacsStoreResult.Details}");
        return BuildSnapshot();
    }

    /// <summary>
    /// 查询 Orthanc 中最近的检查列表。
    /// </summary>
    public async Task<ConsoleSnapshot> QueryPacsStudiesAsync(PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        _statusText = "正在查询 PACS 检查";
        _notesText = $"使用 Orthanc REST 查询 {_pacsConfiguration.Host}:{_pacsConfiguration.RestApiPort}";
        _auditService.Record("PACS 查询开始");

        var queryResult = await _pacsStoreService.QueryStudiesAsync(_pacsConfiguration, criteria, cancellationToken);
        _remoteStudies = queryResult.Studies;
        _statusText = queryResult.StatusText;
        _notesText = queryResult.Details;
        _auditService.Record($"PACS 查询{(queryResult.IsSuccess ? "成功" : "失败")}: {queryResult.Details}");
        return BuildSnapshot();
    }

    /// <summary>
    /// 使用标准 DICOM C-FIND 查询远端检查。
    /// </summary>
    public async Task<ConsoleSnapshot> QueryPacsStudiesViaDicomAsync(PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        _statusText = "正在执行 C-FIND 查询";
        _notesText = $"使用 DICOM 查询 {_pacsConfiguration.Host}:{_pacsConfiguration.Port}";
        _auditService.Record("C-FIND 查询开始");

        var queryResult = await _pacsStoreService.QueryStudiesViaDicomAsync(_pacsConfiguration, criteria, cancellationToken);
        _remoteStudies = queryResult.Studies;
        _statusText = queryResult.StatusText;
        _notesText = queryResult.Details;
        _auditService.Record($"C-FIND 查询{(queryResult.IsSuccess ? "成功" : "失败")}: {queryResult.Details}");
        return BuildSnapshot();
    }

    /// <summary>
    /// 将指定远端检查回取到本地目录。
    /// </summary>
    public async Task<(ConsoleSnapshot Snapshot, string? ImportedDirectoryPath)> RetrievePacsStudyAsync(string remoteStudyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remoteStudyId))
        {
            _statusText = "PACS 回取失败";
            _notesText = "未选择可回取的远端检查。";
            _auditService.Record("PACS 回取失败: 未选择远端检查");
            return (BuildSnapshot(), null);
        }

        var targetDirectory = Path.Combine(_pacsConfiguration.OutputDirectory, "retrieved", remoteStudyId);
        _statusText = "正在回取 PACS 检查";
        _notesText = $"保存到 {targetDirectory}";
        _auditService.Record($"PACS 回取开始: {remoteStudyId}");

        var retrieveResult = await _pacsStoreService.RetrieveStudyAsync(remoteStudyId, targetDirectory, _pacsConfiguration, cancellationToken);
        _statusText = retrieveResult.StatusText;
        _notesText = retrieveResult.Details;
        _auditService.Record($"PACS 回取{(retrieveResult.IsSuccess ? "成功" : "失败")}: {retrieveResult.Details}");
        return (BuildSnapshot(), retrieveResult.IsSuccess ? retrieveResult.ImportedDirectoryPath : null);
    }

    /// <summary>
    /// 使用标准 DICOM C-MOVE 将远端检查推送到本地接收端。
    /// </summary>
    public async Task<(ConsoleSnapshot Snapshot, string? ImportedDirectoryPath)> RetrievePacsStudyViaDicomAsync(string studyInstanceUid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            _statusText = "C-MOVE 回取失败";
            _notesText = "未选择可回取的远端检查。";
            _auditService.Record("C-MOVE 回取失败: 未选择远端检查");
            return (BuildSnapshot(), null);
        }

        var targetDirectory = Path.Combine(_pacsConfiguration.OutputDirectory, "dicom-move", SanitizePathSegment(studyInstanceUid));
        _statusText = "正在执行 C-MOVE 回取";
        _notesText = $"接收到 {targetDirectory}";
        _auditService.Record($"C-MOVE 回取开始: {studyInstanceUid}");

        var retrieveResult = await _pacsStoreService.RetrieveStudyViaDicomAsync(studyInstanceUid, targetDirectory, _pacsConfiguration, cancellationToken);
        _statusText = retrieveResult.StatusText;
        _notesText = retrieveResult.Details;
        _auditService.Record($"C-MOVE 回取{(retrieveResult.IsSuccess ? "成功" : "失败")}: {retrieveResult.Details}");
        return (BuildSnapshot(), retrieveResult.IsSuccess ? retrieveResult.ImportedDirectoryPath : null);
    }

    private static string SanitizePathSegment(string value)
    {
        return string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// 把当前工作列表、设备状态、联锁结果和审计记录汇总为控制台快照。
    /// </summary>
    private ConsoleSnapshot BuildSnapshot()
    {
        var worklistItems = _orders.Select(order => new WorklistItem(
            order.OrderId,
            order.PatientId,
            order.PatientName,
            order.ProcedureDescription,
            order.BodyPart,
            order.Projection,
            order.ScheduledTime,
            order.Status)).ToArray();

        var historyItems = _examSessionStore
            .GetRecent(HistoryItemLimit)
            .Select(session => new ExamHistoryItem(
                session.SessionId,
                session.PatientName,
                session.ProcedureDescription,
                session.BodyPart,
                session.Projection,
                session.WorkflowStatus,
                session.DeviceState,
                session.UpdatedAtUtc,
                session.LastGeneratedArtifactPath))
            .ToArray();

        var currentPatientText = _selectedOrder is null
            ? "未选择患者"
            : $"{_selectedOrder.PatientName} ({_selectedOrder.PatientId})";

        var currentOrderText = _selectedOrder is null
            ? "未选择检查"
            : $"{_selectedOrder.ProcedureDescription} / {_selectedOrder.BodyPart} / {_selectedOrder.Projection}";

        return new ConsoleSnapshot(
            worklistItems,
            historyItems,
            _remoteStudies,
            _selectedOrder?.OrderId,
            _exposureParameters,
            _exposureParameterRange,
            _pacsConfiguration,
            _session?.DeviceState ?? DeviceOperationalState.Idle,
            _session?.WorkflowStatus,
            _lastInterlockResult.IsPassed,
            currentPatientText,
            currentOrderText,
            _statusText,
            _notesText,
            _lastInterlockResult.Messages,
            _auditService.GetEntries().Select(entry => $"{entry.OccurredAtUtc:O} {entry.Message}").ToArray(),
            _lastExposureResult,
            _lastPacsStoreResult);
    }

    private void PersistSession()
    {
        if (_session is null)
        {
            return;
        }

        _examSessionStore.Save(new ExamSessionRecord(
            _session.SessionId,
            _session.Order.OrderId,
            _session.Order.PatientId,
            _session.Order.PatientName,
            _session.Order.ProcedureDescription,
            _session.Order.BodyPart,
            _session.Order.Projection,
            _session.WorkflowStatus,
            _session.DeviceState,
            _session.StartedAtUtc,
            _session.LastExposureAtUtc,
            _session.LastGeneratedArtifact,
            _lastExposureResult?.ImageId,
            DateTime.UtcNow,
            _session.MppsInstanceUid,
            _session.MppsStatus,
            _session.MppsCreatedAtUtc,
            _session.MppsLastSentAtUtc,
            _session.MppsLastError,
            _session.ScheduledProcedureStepIdSnapshot,
            _session.AccessionNumberSnapshot));
    }
}