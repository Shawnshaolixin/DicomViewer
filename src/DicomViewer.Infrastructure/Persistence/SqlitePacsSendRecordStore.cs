using System.Globalization;
using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqlitePacsSendRecordStore : IPacsSendRecordStore
{
    private readonly IAppDbConnectionFactory _connectionFactory;

    public SqlitePacsSendRecordStore(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Add(PacsSendRecord sendRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PacsSendRecords (
                SessionId, FilePath, IsSuccess, StatusText, Details, CalledAeTitle, Host, Port, ProcessedAtUtc)
            VALUES (
                $sessionId, $filePath, $isSuccess, $statusText, $details, $calledAeTitle, $host, $port, $processedAtUtc);
            """;

        AddParameter(command, "$sessionId", sendRecord.SessionId);
        AddParameter(command, "$filePath", sendRecord.FilePath);
        AddParameter(command, "$isSuccess", sendRecord.IsSuccess ? 1 : 0);
        AddParameter(command, "$statusText", sendRecord.StatusText);
        AddParameter(command, "$details", sendRecord.Details);
        AddParameter(command, "$calledAeTitle", sendRecord.CalledAeTitle);
        AddParameter(command, "$host", sendRecord.Host);
        AddParameter(command, "$port", sendRecord.Port);
        AddParameter(command, "$processedAtUtc", sendRecord.ProcessedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<PacsSendRecord> GetBySessionId(string sessionId)
    {
        var sendRecords = new List<PacsSendRecord>();

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, FilePath, IsSuccess, StatusText, Details, CalledAeTitle, Host, Port, ProcessedAtUtc
            FROM PacsSendRecords
            WHERE SessionId = $sessionId
            ORDER BY Id ASC;
            """;
        AddParameter(command, "$sessionId", sessionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            sendRecords.Add(new PacsSendRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2) == 1,
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetInt32(7),
                DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }

        return sendRecords;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}