namespace DicomViewer.Domain.Entities;

public sealed record AuditEntry(DateTime OccurredAtUtc, string Message);