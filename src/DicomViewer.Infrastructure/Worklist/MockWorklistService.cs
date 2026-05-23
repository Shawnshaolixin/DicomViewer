using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Worklist;

/// <summary>
/// 提供固定的模拟工作列表数据，便于控制台在脱离 HIS/RIS 时演示流程。
/// </summary>
public sealed class MockWorklistService : IWorklistService
{
    /// <summary>
    /// 返回一组预置检查任务。
    /// </summary>
    public Task<IReadOnlyList<ImagingOrder>> LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ImagingOrder> orders =
        [
            new ImagingOrder
            {
                OrderId = "ORD-1001",
                PatientId = "P-0001",
                PatientName = "Zhang San",
                ProcedureDescription = "Chest PA",
                BodyPart = "CHEST",
                Projection = "PA",
                ScheduledTime = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Local),
                Status = "Scheduled",
            },
            new ImagingOrder
            {
                OrderId = "ORD-1002",
                PatientId = "P-0002",
                PatientName = "Li Si",
                ProcedureDescription = "Knee AP",
                BodyPart = "KNEE",
                Projection = "AP",
                ScheduledTime = new DateTime(2026, 5, 23, 9, 20, 0, DateTimeKind.Local),
                Status = "Waiting",
            },
            new ImagingOrder
            {
                OrderId = "ORD-1003",
                PatientId = "P-0003",
                PatientName = "Wang Wu",
                ProcedureDescription = "Abdomen Supine",
                BodyPart = "ABDOMEN",
                Projection = "AP",
                ScheduledTime = new DateTime(2026, 5, 23, 9, 45, 0, DateTimeKind.Local),
                Status = "Scheduled",
            },
        ];

        return Task.FromResult(orders);
    }
}