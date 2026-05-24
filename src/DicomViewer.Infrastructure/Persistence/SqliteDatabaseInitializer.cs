using DicomViewer.Application.Abstractions;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqliteDatabaseInitializer
{
    private readonly IAppDbConnectionFactory _connectionFactory;

    public SqliteDatabaseInitializer(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void EnsureCreated()
    {
        var databaseDirectory = Path.GetDirectoryName(_connectionFactory.DatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AuditEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OccurredAtUtc TEXT NOT NULL,
                Message TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}