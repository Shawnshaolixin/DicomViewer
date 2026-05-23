namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示一个患者及其名下的全部检查。
/// </summary>
public sealed class Patient
{
    public required string PatientId { get; init; }

    public required string PatientName { get; init; }

    public IReadOnlyList<Study> Studies { get; init; } = Array.Empty<Study>();
}