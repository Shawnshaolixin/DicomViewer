using DicomViewer.Application.Models;

namespace DicomViewer.Application.Abstractions;

public interface IPacsSendRecordStore
{
    void Add(PacsSendRecord sendRecord);

    IReadOnlyList<PacsSendRecord> GetBySessionId(string sessionId);
}