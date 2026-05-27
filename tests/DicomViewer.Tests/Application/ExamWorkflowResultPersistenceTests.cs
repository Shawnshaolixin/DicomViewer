using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;
using DicomViewer.Infrastructure.Services;

namespace DicomViewer.Tests.Application;

public sealed class ExamWorkflowResultPersistenceTests
{
    [Fact]
    public async Task SelectOrder_PersistsReadySessionState()
    {
        var examSessionStore = new InMemoryExamSessionStore();
        var service = CreateService(examSessionStore, new InMemoryPacsSendRecordStore());

        _ = await service.LoadWorklistAsync();
        var snapshot = service.SelectOrder("ORD-1");

        var persistedSession = examSessionStore.GetBySessionId(snapshot.SelectedOrderId is null ? string.Empty : examSessionStore.LastSessionId!);

        Assert.NotNull(persistedSession);
        Assert.Equal(ExamWorkflowStatus.Ready, persistedSession!.WorkflowStatus);
        Assert.Equal(DeviceOperationalState.Ready, persistedSession.DeviceState);
    }

    [Fact]
    public async Task ExecuteExposureAsync_PersistsGeneratedArtifact()
    {
        var examSessionStore = new InMemoryExamSessionStore();
        var service = CreateService(examSessionStore, new InMemoryPacsSendRecordStore());

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");
        var snapshot = await service.ExecuteExposureAsync();

        var persistedSession = examSessionStore.GetBySessionId(examSessionStore.LastSessionId!);

        Assert.NotNull(persistedSession);
        Assert.Equal(snapshot.LastExposureResult!.ArtifactPath, persistedSession!.LastGeneratedArtifactPath);
        Assert.NotNull(persistedSession.LastExposureAtUtc);
    }

    [Fact]
    public async Task SendToPacsAsync_PersistsSendRecord()
    {
        var pacsSendRecordStore = new InMemoryPacsSendRecordStore();
        var examSessionStore = new InMemoryExamSessionStore();
        var service = CreateService(examSessionStore, pacsSendRecordStore);

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");
        _ = await service.ExecuteExposureAsync();
        _ = await service.SendToPacsAsync();

        var sendRecords = pacsSendRecordStore.GetBySessionId(examSessionStore.LastSessionId!);

        Assert.Single(sendRecords);
        Assert.True(sendRecords[0].IsSuccess);
        Assert.Equal("PACS 发送成功", sendRecords[0].StatusText);
    }

    [Fact]
    public async Task ExecuteExposureAsync_IncludesPersistedSessionInHistory()
    {
        var examSessionStore = new InMemoryExamSessionStore();
        var service = CreateService(examSessionStore, new InMemoryPacsSendRecordStore());

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");
        var snapshot = await service.ExecuteExposureAsync();

        Assert.NotEmpty(snapshot.HistoryItems);
        Assert.Equal("Demo Patient", snapshot.HistoryItems[0].PatientName);
        Assert.Equal(snapshot.LastExposureResult!.ArtifactPath, snapshot.HistoryItems[0].ArtifactPath);
    }

    private static ExamWorkflowService CreateService(InMemoryExamSessionStore examSessionStore, InMemoryPacsSendRecordStore pacsSendRecordStore)
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            new FakeExposureSimulationService(),
            new SuccessfulPacsStoreService(),
            new InMemoryAuditService(),
            new InMemoryConsoleConfigurationStore(),
            examSessionStore,
            pacsSendRecordStore);
    }

    private sealed class FixedWorklistService : IWorklistService
    {
        public Task<IReadOnlyList<ImagingOrder>> QueryAsync(MwlQueryCriteria criteria, CancellationToken cancellationToken = default)
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

    private sealed class SuccessfulPacsStoreService : IPacsStoreService
    {
        public Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(true, "ok", "ok", configuration.CalledAeTitle, configuration.Host, configuration.Port, string.Empty, DateTime.UtcNow));
        }

        public Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(true, "PACS 发送成功", "Orthanc 已确认接收。", configuration.CalledAeTitle, configuration.Host, configuration.Port, dicomFilePath, DateTime.UtcNow));
        }

        public Task<PacsStudyQueryResult> QueryStudiesAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStudyQueryResult(true, "ok", "ok", Array.Empty<PacsRemoteStudy>(), DateTime.UtcNow));
        }

        public Task<PacsRetrieveResult> RetrieveStudyAsync(string remoteStudyId, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsRetrieveResult(true, "ok", "ok", targetDirectory, DateTime.UtcNow));
        }

        public Task<PacsStudyQueryResult> QueryStudiesViaDicomAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStudyQueryResult(true, "ok", "ok", Array.Empty<PacsRemoteStudy>(), DateTime.UtcNow));
        }

        public Task<PacsRetrieveResult> RetrieveStudyViaDicomAsync(string studyInstanceUid, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsRetrieveResult(true, "ok", "ok", targetDirectory, DateTime.UtcNow));
        }
    }

    private sealed class InMemoryConsoleConfigurationStore : IConsoleConfigurationStore
    {
        public ConsoleConfiguration Load() => ConsoleConfiguration.Default;

        public void Save(ConsoleConfiguration configuration)
        {
        }
    }

    private sealed class InMemoryExamSessionStore : IExamSessionStore
    {
        private readonly Dictionary<string, ExamSessionRecord> _sessions = new();

        public string? LastSessionId { get; private set; }

        public void Save(ExamSessionRecord sessionRecord)
        {
            LastSessionId = sessionRecord.SessionId;
            _sessions[sessionRecord.SessionId] = sessionRecord;
        }

        public ExamSessionRecord? GetBySessionId(string sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        public IReadOnlyList<ExamSessionRecord> GetRecent(int limit)
        {
            return _sessions.Values
                .OrderByDescending(session => session.UpdatedAtUtc)
                .Take(limit)
                .ToArray();
        }
    }

    private sealed class InMemoryPacsSendRecordStore : IPacsSendRecordStore
    {
        private readonly List<PacsSendRecord> _records = [];

        public void Add(PacsSendRecord sendRecord)
        {
            _records.Add(sendRecord);
        }

        public IReadOnlyList<PacsSendRecord> GetBySessionId(string sessionId)
        {
            return _records.Where(record => record.SessionId == sessionId).ToArray();
        }
    }
}