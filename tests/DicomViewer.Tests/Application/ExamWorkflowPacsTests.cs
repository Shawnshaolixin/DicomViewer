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

    private static ExamWorkflowService CreateService(IPacsStoreService pacsStoreService)
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            new FakeExposureSimulationService(),
            pacsStoreService,
            new InMemoryAuditService());
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
    }
}