using DicomViewer.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqliteAppDbConnectionFactory : IAppDbConnectionFactory
{
    public SqliteAppDbConnectionFactory(string? databasePath = null)
    {
        DatabasePath = string.IsNullOrWhiteSpace(databasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DicomViewer",
                "dicomviewer.db")
            : databasePath;
    }

    public string DatabasePath { get; }

    public System.Data.Common.DbConnection CreateConnection()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
        };

        return new SqliteConnection(connectionStringBuilder.ConnectionString);
    }
}