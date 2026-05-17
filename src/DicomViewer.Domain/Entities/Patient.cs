namespace DicomViewer.Domain.Entities;

public sealed class Patient
{
    public required string PatientId { get; init; }

    public required string PatientName { get; init; }

    public IReadOnlyList<Study> Studies { get; init; } = Array.Empty<Study>();
}