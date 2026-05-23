using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Services;

public sealed class ExamWorkflowService
{
    private readonly IWorklistService _worklistService;
    private readonly IInterlockService _interlockService;
    private readonly IExposureSimulationService _exposureSimulationService;
    private readonly IPacsStoreService _pacsStoreService;
    private readonly IAuditService _auditService;

    private IReadOnlyList<ImagingOrder> _orders = Array.Empty<ImagingOrder>();
    private ImagingOrder? _selectedOrder;
    private ExamSession? _session;
    private ExposureParameters _exposureParameters = ExposureParameters.Default;
    private ExposureParameterRange _exposureParameterRange = ExposureParameterRange.Default;
    private InterlockCheckResult _lastInterlockResult = InterlockCheckResult.Fail((InterlockCode.NoActiveOrder, "未选择检查任务。"));
    private ExposureResult? _lastExposureResult;
    private PacsStoreResult? _lastPacsStoreResult;
    private PacsConfiguration _pacsConfiguration = PacsConfiguration.Default;
    private string _statusText = "控制台尚未初始化";
    private string _notesText = "请先加载工作列表。";

    public ExamWorkflowService(
        IWorklistService worklistService,
        IInterlockService interlockService,
        IExposureSimulationService exposureSimulationService,
        IPacsStoreService pacsStoreService,
        IAuditService auditService)
    {
        _worklistService = worklistService;
        _interlockService = interlockService;
        _exposureSimulationService = exposureSimulationService;
        _pacsStoreService = pacsStoreService;
        _auditService = auditService;
    }

    public bool DetectorConnected { get; private set; } = true;

    public bool TubeWarmedUp { get; private set; } = true;

    public bool DoorClosed { get; private set; } = true;

    public bool PacsAvailable { get; private set; } = true;

    public async Task<ConsoleSnapshot> LoadWorklistAsync(CancellationToken cancellationToken = default)
    {
        _orders = await _worklistService.LoadAsync(cancellationToken);
        _statusText = _orders.Count == 0 ? "工作列表为空" : $"已加载 {_orders.Count} 条工作列表";
        _notesText = _orders.Count == 0 ? "请检查模拟数据源。" : "请选择一条检查任务进入准备状态。";
        _auditService.Record(_statusText);
        return BuildSnapshot();
    }

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
            null);

        _statusText = "已创建检查会话";
        _notesText = $"当前任务: {_selectedOrder.PatientName} / {_selectedOrder.ProcedureDescription}";
        _auditService.Record($"选择检查任务: {_selectedOrder.OrderId}");
        return RunInterlockCheck();
    }

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

    public ConsoleSnapshot UpdatePacsConfiguration(PacsConfiguration pacsConfiguration)
    {
        _pacsConfiguration = pacsConfiguration;
        _statusText = "PACS 配置已更新";
        _notesText = $"{pacsConfiguration.CallingAeTitle} -> {pacsConfiguration.CalledAeTitle} @ {pacsConfiguration.Host}:{pacsConfiguration.Port}";
        _auditService.Record("修改 PACS 配置");
        return BuildSnapshot();
    }

    public ConsoleSnapshot UpdateExposureParameterRange(ExposureParameterRange exposureParameterRange)
    {
        _exposureParameterRange = exposureParameterRange;
        _statusText = "参数范围已更新";
        _notesText = $"kV={exposureParameterRange.MinKilovoltagePeak:0.#}-{exposureParameterRange.MaxKilovoltagePeak:0.#}, mA={exposureParameterRange.MinTubeCurrentMilliampere:0.#}-{exposureParameterRange.MaxTubeCurrentMilliampere:0.#}";
        _auditService.Record("修改参数范围");
        return BuildSnapshot();
    }

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

        return BuildSnapshot();
    }

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

        _statusText = "模拟曝光完成";
        _notesText = _lastExposureResult.PreviewText;
        _auditService.Record("曝光执行完成");

        return BuildSnapshot();
    }

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
        }

        _statusText = _lastPacsStoreResult.StatusText;
        _notesText = _lastPacsStoreResult.Details;
        _auditService.Record($"PACS 发送{(_lastPacsStoreResult.IsSuccess ? "成功" : "失败")}: {_lastPacsStoreResult.Details}");
        return BuildSnapshot();
    }

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

        var currentPatientText = _selectedOrder is null
            ? "未选择患者"
            : $"{_selectedOrder.PatientName} ({_selectedOrder.PatientId})";

        var currentOrderText = _selectedOrder is null
            ? "未选择检查"
            : $"{_selectedOrder.ProcedureDescription} / {_selectedOrder.BodyPart} / {_selectedOrder.Projection}";

        return new ConsoleSnapshot(
            worklistItems,
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
}