using DicomViewer.Infrastructure.Persistence;

namespace DicomViewer.Tests.Infrastructure;

public sealed class SqliteAuditServiceTests
{
    [Fact]
    public void RecordAndGetEntries_PersistsAuditEntriesInOrder()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var databasePath = Path.Combine(tempDirectory.FullName, "dicomviewer.db");
        var connectionFactory = new SqliteAppDbConnectionFactory(databasePath);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);
        var auditService = new SqliteAuditService(connectionFactory);

        try
        {
            databaseInitializer.EnsureCreated();

            auditService.Record("first");
            auditService.Record("second");

            var entries = auditService.GetEntries();

            Assert.Collection(
                entries,
                entry => Assert.Equal("first", entry.Message),
                entry => Assert.Equal("second", entry.Message));
            Assert.All(entries, entry => Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind));
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}