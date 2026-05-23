using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Worklist;

public sealed class MockWorklistService : IWorklistService
{
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