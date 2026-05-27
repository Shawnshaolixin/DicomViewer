using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;
using DicomViewer.Infrastructure.Services;

namespace DicomViewer.Tests.Application;

public sealed class ExamWorkflowPacsTests
{
    [Fact]
    public async Task SendToPacsAsync_WithExposureResult_CompletesSessionAndStoresResult()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");
        _ = await service.ExecuteExposureAsync();

        var snapshot = await service.SendToPacsAsync();

        Assert.Equal(ExamWorkflowStatus.Completed, snapshot.WorkflowStatus);
        Assert.NotNull(snapshot.LastPacsStoreResult);
        Assert.True(snapshot.LastPacsStoreResult!.IsSuccess);
        Assert.Contains("PACS 发送成功", snapshot.AuditEntries.Last(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendToPacsAsync_WithoutExposureResult_FailsGracefully()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        _ = await service.LoadWorklistAsync();
        _ = service.SelectOrder("ORD-1");

        var snapshot = await service.SendToPacsAsync();

        Assert.Equal("PACS 发送失败", snapshot.StatusText);
        Assert.Null(snapshot.LastPacsStoreResult);
    }

    [Fact]
    public async Task VerifyPacsConnectionAsync_UsesPacsServiceAndReturnsStatus()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        var snapshot = await service.VerifyPacsConnectionAsync();

        Assert.NotNull(snapshot.LastPacsStoreResult);
        Assert.Equal("PACS 连通性验证成功", snapshot.StatusText);
    }

    [Fact]
    public async Task QueryPacsStudiesAsync_LoadsRemoteStudiesIntoSnapshot()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        var snapshot = await service.QueryPacsStudiesAsync(PacsStudyQueryCriteria.Empty);

        Assert.Equal("PACS 查询成功", snapshot.StatusText);
        Assert.Single(snapshot.RemoteStudies);
        Assert.Equal("REMOTE-1", snapshot.RemoteStudies[0].RemoteStudyId);
    }

    [Fact]
    public async Task QueryPacsStudiesViaDicomAsync_LoadsRemoteStudiesIntoSnapshot()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        var snapshot = await service.QueryPacsStudiesViaDicomAsync(PacsStudyQueryCriteria.Empty);

        Assert.Equal("C-FIND 查询成功", snapshot.StatusText);
        Assert.Single(snapshot.RemoteStudies);
        Assert.Equal("1.2.3", snapshot.RemoteStudies[0].StudyInstanceUid);
    }

    [Fact]
    public async Task RetrievePacsStudyAsync_ReturnsImportedDirectoryPath()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        var result = await service.RetrievePacsStudyAsync("REMOTE-1");

        Assert.Equal("PACS 回取成功", result.Snapshot.StatusText);
        Assert.EndsWith(Path.Combine("retrieved", "REMOTE-1"), result.ImportedDirectoryPath);
    }

    [Fact]
    public async Task RetrievePacsStudyViaDicomAsync_ReturnsImportedDirectoryPath()
    {
        var service = CreateService(new SuccessfulPacsStoreService());

        var result = await service.RetrievePacsStudyViaDicomAsync("1.2.3");

        Assert.Equal("C-MOVE 回取成功", result.Snapshot.StatusText);
        Assert.EndsWith(Path.Combine("dicom-move", "1.2.3"), result.ImportedDirectoryPath);
    }

    private static ExamWorkflowService CreateService(IPacsStoreService pacsStoreService)
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            new FakeExposureSimulationService(),
            pacsStoreService,
            new InMemoryAuditService(),
            new InMemoryConsoleConfigurationStore(),
            new InMemoryExamSessionStore(),
            new InMemoryPacsSendRecordStore());
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
            return Task.FromResult(new PacsStoreResult(
                true,
                "PACS 连通性验证成功",
                "Orthanc 已响应 C-ECHO。",
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
                "Orthanc 已确认接收。",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                dicomFilePath,
                DateTime.UtcNow));
        }

        public Task<PacsStudyQueryResult> QueryStudiesAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PacsRemoteStudy> studies =
            [
                new PacsRemoteStudy("REMOTE-1", "1.2.3", "P-1", "Demo Patient", "Chest PA", "DX", 1, new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc)),
            ];

            return Task.FromResult(new PacsStudyQueryResult(true, "PACS 查询成功", "共查询到 1 条远端检查。", studies, DateTime.UtcNow));
        }

        public Task<PacsRetrieveResult> RetrieveStudyAsync(string remoteStudyId, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsRetrieveResult(true, "PACS 回取成功", $"已下载到 {targetDirectory}", targetDirectory, DateTime.UtcNow));
        }

        public Task<PacsStudyQueryResult> QueryStudiesViaDicomAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PacsRemoteStudy> studies =
            [
                new PacsRemoteStudy(string.Empty, "1.2.3", "P-1", "Demo Patient", "Chest PA", "DX", 1, new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc)),
            ];

            return Task.FromResult(new PacsStudyQueryResult(true, "C-FIND 查询成功", "共查询到 1 条远端检查。", studies, DateTime.UtcNow));
        }

        public Task<PacsRetrieveResult> RetrieveStudyViaDicomAsync(string studyInstanceUid, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsRetrieveResult(true, "C-MOVE 回取成功", $"已接收到 {targetDirectory}", targetDirectory, DateTime.UtcNow));
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
        public void Save(ExamSessionRecord sessionRecord)
        {
        }

        public ExamSessionRecord? GetBySessionId(string sessionId)
        {
            return null;
        }

        public IReadOnlyList<ExamSessionRecord> GetRecent(int limit)
        {
            return Array.Empty<ExamSessionRecord>();
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