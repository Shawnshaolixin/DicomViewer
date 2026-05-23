namespace DicomViewer.Domain.Entities;

/// <summary>
/// 表示一条带时间戳的审计日志。
/// </summary>
public sealed record AuditEntry(DateTime OccurredAtUtc, string Message);