using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Application.Services;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;
using DicomViewer.Infrastructure.Services;

namespace DicomViewer.Tests.Application;

public sealed class ExamWorkflowConfigurationPersistenceTests
{
    [Fact]
    public async Task LoadWorklistAsync_UsesPersistedConsoleConfiguration()
    {
        var configurationStore = new InMemoryConsoleConfigurationStore
        {
            Configuration = new ConsoleConfiguration(
                new PacsConfiguration("LOCALAE", "ORTHANCAE", "192.168.0.10", 11112, 8042, @"D:\SavedOutput"),
                new ExposureParameterRange(50, 120, 20, 300, 2, 500, 0.5, 100, 800, 1800))
        };

        var service = CreateService(configurationStore);

        var snapshot = await service.LoadWorklistAsync();

        Assert.Equal("LOCALAE", snapshot.PacsConfiguration.CallingAeTitle);
        Assert.Equal(@"D:\SavedOutput", snapshot.PacsConfiguration.OutputDirectory);
        Assert.Equal(50, snapshot.ExposureParameterRange.MinKilovoltagePeak);
        Assert.Equal(1800, snapshot.ExposureParameterRange.MaxSourceToImageDistanceMillimeter);
    }

    [Fact]
    public async Task UpdateConfiguration_SavesToConfigurationStore()
    {
        var configurationStore = new InMemoryConsoleConfigurationStore();
        var service = CreateService(configurationStore);

        _ = await service.LoadWorklistAsync();
        _ = service.UpdatePacsConfiguration(new PacsConfiguration("CALLING", "CALLED", "10.0.0.2", 104, 8042, @"D:\Output"));
        _ = service.UpdateExposureParameterRange(new ExposureParameterRange(45, 130, 15, 450, 1, 900, 0.2, 300, 700, 1900));

        Assert.Equal("CALLING", configurationStore.Configuration.PacsConfiguration.CallingAeTitle);
        Assert.Equal(@"D:\Output", configurationStore.Configuration.PacsConfiguration.OutputDirectory);
        Assert.Equal(45, configurationStore.Configuration.ExposureParameterRange.MinKilovoltagePeak);
        Assert.Equal(1900, configurationStore.Configuration.ExposureParameterRange.MaxSourceToImageDistanceMillimeter);
    }

    private static ExamWorkflowService CreateService(IConsoleConfigurationStore configurationStore)
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            new FakeExposureSimulationService(),
            new FakePacsStoreService(),
            new InMemoryAuditService(),
            configurationStore,
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

    private sealed class FakePacsStoreService : IPacsStoreService
    {
        public Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(true, "ok", "ok", configuration.CalledAeTitle, configuration.Host, configuration.Port, string.Empty, DateTime.UtcNow));
        }

        public Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PacsStoreResult(true, "ok", "ok", configuration.CalledAeTitle, configuration.Host, configuration.Port, dicomFilePath, DateTime.UtcNow));
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
        public ConsoleConfiguration Configuration { get; set; } = ConsoleConfiguration.Default;

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