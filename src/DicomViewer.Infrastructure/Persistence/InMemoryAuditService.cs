using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class InMemoryAuditService : IAuditService
{
    private readonly List<AuditEntry> _entries = new();

    public void Record(string message)
    {
        _entries.Add(new AuditEntry(DateTime.UtcNow, message));
    }

    public IReadOnlyList<AuditEntry> GetEntries()
    {
        return _entries;
    }
}