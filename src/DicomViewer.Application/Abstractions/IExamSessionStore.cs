using DicomViewer.Application.Models;

namespace DicomViewer.Application.Abstractions;

public interface IExamSessionStore
{
    void Save(ExamSessionRecord sessionRecord);

    ExamSessionRecord? GetBySessionId(string sessionId);

    IReadOnlyList<ExamSessionRecord> GetRecent(int limit);
}