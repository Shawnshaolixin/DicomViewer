namespace DicomViewer.Domain.Entities;

public sealed class ImagingOrder
{
    public required string OrderId { get; init; }

    public required string PatientId { get; init; }

    public required string PatientName { get; init; }

    public required string ProcedureDescription { get; init; }

    public required string BodyPart { get; init; }

    public required string Projection { get; init; }

    public required DateTime ScheduledTime { get; init; }

    public string Status { get; init; } = "Scheduled";
}