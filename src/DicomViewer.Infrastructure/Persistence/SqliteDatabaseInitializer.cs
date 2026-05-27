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

            CREATE TABLE IF NOT EXISTS ExamSessions (
                SessionId TEXT PRIMARY KEY,
                OrderId TEXT NOT NULL,
                PatientId TEXT NOT NULL,
                PatientName TEXT NOT NULL,
                ProcedureDescription TEXT NOT NULL,
                BodyPart TEXT NOT NULL,
                Projection TEXT NOT NULL,
                WorkflowStatus INTEGER NOT NULL,
                DeviceState INTEGER NOT NULL,
                StartedAtUtc TEXT NOT NULL,
                LastExposureAtUtc TEXT NULL,
                LastGeneratedArtifactPath TEXT NULL,
                LastImageId TEXT NULL,
                MppsInstanceUid TEXT NULL,
                MppsStatus INTEGER NOT NULL DEFAULT 0,
                MppsCreatedAtUtc TEXT NULL,
                MppsLastSentAtUtc TEXT NULL,
                MppsLastError TEXT NULL,
                ScheduledProcedureStepIdSnapshot TEXT NULL,
                AccessionNumberSnapshot TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PacsSendRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                IsSuccess INTEGER NOT NULL,
                StatusText TEXT NOT NULL,
                Details TEXT NOT NULL,
                CalledAeTitle TEXT NOT NULL,
                Host TEXT NOT NULL,
                Port INTEGER NOT NULL,
                ProcessedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, "ExamSessions", "MppsInstanceUid", "TEXT NULL");
        EnsureColumnExists(connection, "ExamSessions", "MppsStatus", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "ExamSessions", "MppsCreatedAtUtc", "TEXT NULL");
        EnsureColumnExists(connection, "ExamSessions", "MppsLastSentAtUtc", "TEXT NULL");
        EnsureColumnExists(connection, "ExamSessions", "MppsLastError", "TEXT NULL");
        EnsureColumnExists(connection, "ExamSessions", "ScheduledProcedureStepIdSnapshot", "TEXT NULL");
        EnsureColumnExists(connection, "ExamSessions", "AccessionNumberSnapshot", "TEXT NULL");
    }

    private static void EnsureColumnExists(System.Data.Common.DbConnection connection, string tableName, string columnName, string columnDefinition)
    {
        using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = pragmaCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}