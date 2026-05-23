using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 提供审计记录的写入与读取能力。
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 记录一条审计消息。
    /// </summary>
    void Record(string message);

    /// <summary>
    /// 获取当前已记录的审计条目。
    /// </summary>
    IReadOnlyList<AuditEntry> GetEntries();
}