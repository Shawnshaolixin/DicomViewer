using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IAuditService
{
    void Record(string message);

    IReadOnlyList<AuditEntry> GetEntries();
}