using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Persistence;

/// <summary>
/// 使用内存列表保存审计信息，适合学习、调试和单进程演示场景。
/// </summary>
public sealed class InMemoryAuditService : IAuditService
{
    private readonly List<AuditEntry> _entries = new();

    /// <summary>
    /// 追加一条带 UTC 时间戳的审计记录。
    /// </summary>
    public void Record(string message)
    {
        _entries.Add(new AuditEntry(DateTime.UtcNow, message));
    }

    /// <summary>
    /// 返回当前累计的全部审计记录。
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntries()
    {
        return _entries;
    }
}