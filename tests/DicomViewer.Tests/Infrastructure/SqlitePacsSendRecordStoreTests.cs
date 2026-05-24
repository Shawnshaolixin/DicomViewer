using DicomViewer.Application.Models;
using DicomViewer.Infrastructure.Persistence;

namespace DicomViewer.Tests.Infrastructure;

public sealed class SqlitePacsSendRecordStoreTests
{
    [Fact]
    public void AddAndGetBySessionId_RoundTripsSendRecord()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var databasePath = Path.Combine(tempDirectory.FullName, "dicomviewer.db");
        var connectionFactory = new SqliteAppDbConnectionFactory(databasePath);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);
        var store = new SqlitePacsSendRecordStore(connectionFactory);
        var record = new PacsSendRecord(
            "session-1",
            @"D:\output\SIM-1.dcm",
            true,
            "PACS 发送成功",
            "Orthanc 已确认接收。",
            "ORTHANC",
            "127.0.0.1",
            4242,
            new DateTime(2026, 5, 23, 1, 10, 0, DateTimeKind.Utc));

        try
        {
            databaseInitializer.EnsureCreated();

            store.Add(record);
            var loaded = store.GetBySessionId(record.SessionId);

            Assert.Single(loaded);
            Assert.Equal(record, loaded[0]);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}