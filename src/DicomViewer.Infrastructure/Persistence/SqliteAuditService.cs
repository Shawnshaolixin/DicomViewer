using System.Globalization;
using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqliteAuditService : IAuditService
{
    private readonly IAppDbConnectionFactory _connectionFactory;

    public SqliteAuditService(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Record(string message)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO AuditEntries (OccurredAtUtc, Message) VALUES ($occurredAtUtc, $message);";

        var occurredAtParameter = command.CreateParameter();
        occurredAtParameter.ParameterName = "$occurredAtUtc";
        occurredAtParameter.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        command.Parameters.Add(occurredAtParameter);

        var messageParameter = command.CreateParameter();
        messageParameter.ParameterName = "$message";
        messageParameter.Value = message;
        command.Parameters.Add(messageParameter);

        command.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEntry> GetEntries()
    {
        var entries = new List<AuditEntry>();

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT OccurredAtUtc, Message FROM AuditEntries ORDER BY Id ASC;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var occurredAtUtcText = reader.GetString(0);
            var occurredAtUtc = DateTime.Parse(
                occurredAtUtcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);

            entries.Add(new AuditEntry(occurredAtUtc, reader.GetString(1)));
        }

        return entries;
    }
}