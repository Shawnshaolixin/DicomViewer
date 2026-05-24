using DicomViewer.Application.Models;
using DicomViewer.Domain.Enums;
using DicomViewer.Infrastructure.Persistence;

namespace DicomViewer.Tests.Infrastructure;

public sealed class SqliteExamSessionStoreTests
{
    [Fact]
    public void SaveAndGetBySessionId_RoundTripsSessionRecord()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var databasePath = Path.Combine(tempDirectory.FullName, "dicomviewer.db");
        var connectionFactory = new SqliteAppDbConnectionFactory(databasePath);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);
        var store = new SqliteExamSessionStore(connectionFactory);
        var session = new ExamSessionRecord(
            "session-1",
            "ORD-1",
            "P-1",
            "Demo Patient",
            "Chest PA",
            "CHEST",
            "PA",
            ExamWorkflowStatus.Ready,
            DeviceOperationalState.Ready,
            new DateTime(2026, 5, 23, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 23, 1, 5, 0, DateTimeKind.Utc),
            @"D:\output\SIM-1.dcm",
            "SIM-1",
            new DateTime(2026, 5, 23, 1, 6, 0, DateTimeKind.Utc));

        try
        {
            databaseInitializer.EnsureCreated();

            store.Save(session);
            var loaded = store.GetBySessionId(session.SessionId);

            Assert.Equal(session, loaded);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}