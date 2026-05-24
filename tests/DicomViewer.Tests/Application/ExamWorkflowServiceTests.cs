using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;
using DicomViewer.Infrastructure.Services;

namespace DicomViewer.Tests.Application;

public sealed class ExamWorkflowServiceTests
{
    [Fact]
    public async Task LoadWorklistAsync_LoadsMockOrdersAndWritesAudit()
    {
        var service = CreateService();

        var snapshot = await service.LoadWorklistAsync();

        Assert.Equal(2, snapshot.WorklistItems.Count);
        Assert.Contains(snapshot.AuditEntries, entry => entry.Contains("已加载 2 条工作列表", StringComparison.Ordinal));
    }

    [Fact]
    public void RunInterlockCheck_WithoutSelectedOrder_Fails()
    {
        var service = CreateService();

        var snapshot = service.RunInterlockCheck();

        Assert.False(snapshot.CanExpose);
        Assert.Contains("未选择检查任务。", snapshot.InterlockMessages);
        Assert.Equal(DeviceOperationalState.Idle, snapshot.DeviceState);
    }

    [Fact]
    public async Task RunInterlockCheck_WhenParametersOutOfRange_Fails()
    {
        var service = CreateService();

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");

        var snapshot = service.UpdateExposureParameters(ExposureParameters.Default with { KilovoltagePeak = 10 });
        snapshot = service.RunInterlockCheck();

        Assert.False(snapshot.CanExpose);
        Assert.Contains("曝光参数越界。", snapshot.InterlockMessages);
        Assert.Equal(ExamWorkflowStatus.InProgress, snapshot.WorkflowStatus);
    }

    [Fact]
    public async Task RunInterlockCheck_UsesConfiguredParameterRange()
    {
        var service = CreateService();

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");
        _ = service.UpdateExposureParameterRange(ExposureParameterRange.Default with { MaxKilovoltagePeak = 60 });

        var snapshot = service.RunInterlockCheck();

        Assert.False(snapshot.CanExpose);
        Assert.Contains("曝光参数越界。", snapshot.InterlockMessages);
    }

    [Fact]
    public async Task ExecuteExposureAsync_PassesConfiguredOutputDirectory()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();

        try
        {
            var recordingExposureSimulationService = new RecordingExposureSimulationService();
            var service = CreateService(recordingExposureSimulationService: recordingExposureSimulationService);

            _ = await service.LoadWorklistAsync();
            _ = service.SelectOrder("ORD-1");
            _ = service.UpdatePacsConfiguration(PacsConfiguration.Default with { OutputDirectory = outputDirectory.FullName });

            _ = await service.ExecuteExposureAsync();

            Assert.Equal(outputDirectory.FullName, recordingExposureSimulationService.LastOutputDirectory);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task ExecuteExposureAsync_WithValidSelection_CompletesSimulation()
    {
        var service = CreateService();

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");

        var snapshot = await service.ExecuteExposureAsync();

        Assert.True(snapshot.CanExpose);
        Assert.Equal(DeviceOperationalState.Ready, snapshot.DeviceState);
        Assert.Equal(ExamWorkflowStatus.Ready, snapshot.WorkflowStatus);
        Assert.NotNull(snapshot.LastExposureResult);
        Assert.Contains("曝光执行完成", snapshot.AuditEntries.Last(), StringComparison.Ordinal);
    }

    private static ExamWorkflowService CreateService(IExposureSimulationService? recordingExposureSimulationService = null)
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            recordingExposureSimulationService ?? new FakeExposureSimulationService(),
            new FakePacsStoreService(),
            new InMemoryAuditService(),
            new InMemoryConsoleConfigurationStore(),
            new InMemoryExamSessionStore(),
            new InMemoryPacsSendRecordStore());
    }

    private sealed class FixedWorklistService : IWorklistService
    {
        public Task<IReadOnlyList<ImagingOrder>> LoadAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ImagingOrder> orders =
            [
                new ImagingOrder
                {
                    OrderId = "ORD-1",
                    PatientId = "P-1",
                    PatientName = "Demo Patient",
                    ProcedureDescription = "Chest PA",
                    BodyPart = "CHEST",
                    Projection = "PA",
                    ScheduledTime = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Local),
                    Status = "Scheduled",
                },
                new ImagingOrder
                {
                    OrderId = "ORD-2",
                    PatientId = "P-2",
                    PatientName = "Demo Patient 2",
                    ProcedureDescription = "Knee AP",
                    BodyPart = "KNEE",
                    Projection = "AP",
                    ScheduledTime = new DateTime(2026, 5, 23, 10, 0, 0, DateTimeKind.Local),
                    Status = "Waiting",
                },
            ];

            return Task.FromResult(orders);
        }
    }

    private sealed class FakeExposureSimulationService : IExposureSimulationService
    {
        public Task<ExposureResult> RunAsync(ExamSession session, string outputDirectory, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExposureResult(
                "SIM-1",
                "模拟图像已生成",
                Path.Combine(outputDirectory, "SIM-1.dcm"),
                new DateTime(2026, 5, 23, 1, 0, 0, DateTimeKind.Utc)));
        }
    }

    private sealed class RecordingExposureSimulationService : IExposureSimulationService
    {
        public string? LastOutputDirectory { get; private set; }

        public Task<ExposureResult> RunAsync(ExamSession session, string outputDirectory, CancellationToken cancellationToken = default)
        {
            LastOutputDirectory = outputDirectory;
            return Task.FromResult(new ExposureResult(
                "SIM-REC-1",
                "模拟图像已生成",
                Path.Combine(outputDirectory, "SIM-REC-1.dcm"),
                new DateTime(2026, 5, 23, 1, 5, 0, DateTimeKind.Utc)));
        }
    }

    private sealed class FakePacsStoreService : IPacsStoreService
    {
        public Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(
                true,
                "PACS 连通性验证成功",
                "测试替身未执行真实验证。",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                string.Empty,
                DateTime.UtcNow));
        }

        public Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(
                true,
                "PACS 发送成功",
                "测试替身未执行真实发送。",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                dicomFilePath,
                DateTime.UtcNow));
        }
    }

    private sealed class InMemoryConsoleConfigurationStore : IConsoleConfigurationStore
    {
        public ConsoleConfiguration Configuration { get; private set; } = ConsoleConfiguration.Default;

        public ConsoleConfiguration Load()
        {
            return Configuration;
        }

        public void Save(ConsoleConfiguration configuration)
        {
            Configuration = configuration;
        }
    }

    private sealed class InMemoryExamSessionStore : IExamSessionStore
    {
        private readonly Dictionary<string, ExamSessionRecord> _sessions = new();

        public void Save(ExamSessionRecord sessionRecord)
        {
            _sessions[sessionRecord.SessionId] = sessionRecord;
        }

        public ExamSessionRecord? GetBySessionId(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }
    }

    private sealed class InMemoryPacsSendRecordStore : IPacsSendRecordStore
    {
        public void Add(PacsSendRecord sendRecord)
        {
        }

        public IReadOnlyList<PacsSendRecord> GetBySessionId(string sessionId)
        {
            return Array.Empty<PacsSendRecord>();
        }
    }
}