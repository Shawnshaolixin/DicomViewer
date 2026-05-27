using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
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
    public Task<IReadOnlyList<ImagingOrder>> QueryAsync(MwlQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ImagingOrder> orders =
        [
            new ImagingOrder
            {
                OrderId = "ORD-1001",
                PatientId = "P-0001",
                PatientName = "Zhang San",
                AccessionNumber = "ACC-1001",
                RequestedProcedureId = "RP-1001",
                ScheduledProcedureStepId = "SPS-1001",
                StudyInstanceUid = "1.2.826.0.1.3680043.2.1125.1001",
                Modality = "DX",
                ScheduledStationAeTitle = "DICOMVIEWER",
                ScheduledStartDateTime = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Local),
                RequestedProcedureDescription = "Chest PA",
                SourceType = "Mock",
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
                AccessionNumber = "ACC-1002",
                RequestedProcedureId = "RP-1002",
                ScheduledProcedureStepId = "SPS-1002",
                StudyInstanceUid = "1.2.826.0.1.3680043.2.1125.1002",
                Modality = "DX",
                ScheduledStationAeTitle = "DICOMVIEWER",
                ScheduledStartDateTime = new DateTime(2026, 5, 23, 9, 20, 0, DateTimeKind.Local),
                RequestedProcedureDescription = "Knee AP",
                SourceType = "Mock",
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
                AccessionNumber = "ACC-1003",
                RequestedProcedureId = "RP-1003",
                ScheduledProcedureStepId = "SPS-1003",
                StudyInstanceUid = "1.2.826.0.1.3680043.2.1125.1003",
                Modality = "DX",
                ScheduledStationAeTitle = "DICOMVIEWER",
                ScheduledStartDateTime = new DateTime(2026, 5, 23, 9, 45, 0, DateTimeKind.Local),
                RequestedProcedureDescription = "Abdomen Supine",
                SourceType = "Mock",
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