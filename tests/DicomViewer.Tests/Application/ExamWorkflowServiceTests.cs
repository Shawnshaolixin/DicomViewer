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

    private static ExamWorkflowService CreateService()
    {
        return new ExamWorkflowService(
            new FixedWorklistService(),
            new DefaultInterlockService(),
            new FakeExposureSimulationService(),
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
        public Task<ExposureResult> RunAsync(ExamSession session, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExposureResult(
                "SIM-1",
                "模拟图像已生成",
                "simulated-output\\SIM-1.dcm",
                new DateTime(2026, 5, 23, 1, 0, 0, DateTimeKind.Utc)));
        }
    }
}