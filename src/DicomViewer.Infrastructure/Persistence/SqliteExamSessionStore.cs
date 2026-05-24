using System.Globalization;
using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Enums;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class SqliteExamSessionStore : IExamSessionStore
{
    private readonly IAppDbConnectionFactory _connectionFactory;

    public SqliteExamSessionStore(IAppDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Save(ExamSessionRecord sessionRecord)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ExamSessions (
                SessionId, OrderId, PatientId, PatientName, ProcedureDescription, BodyPart, Projection,
                WorkflowStatus, DeviceState, StartedAtUtc, LastExposureAtUtc, LastGeneratedArtifactPath,
                LastImageId, UpdatedAtUtc)
            VALUES (
                $sessionId, $orderId, $patientId, $patientName, $procedureDescription, $bodyPart, $projection,
                $workflowStatus, $deviceState, $startedAtUtc, $lastExposureAtUtc, $lastGeneratedArtifactPath,
                $lastImageId, $updatedAtUtc)
            ON CONFLICT(SessionId) DO UPDATE SET
                OrderId = excluded.OrderId,
                PatientId = excluded.PatientId,
                PatientName = excluded.PatientName,
                ProcedureDescription = excluded.ProcedureDescription,
                BodyPart = excluded.BodyPart,
                Projection = excluded.Projection,
                WorkflowStatus = excluded.WorkflowStatus,
                DeviceState = excluded.DeviceState,
                StartedAtUtc = excluded.StartedAtUtc,
                LastExposureAtUtc = excluded.LastExposureAtUtc,
                LastGeneratedArtifactPath = excluded.LastGeneratedArtifactPath,
                LastImageId = excluded.LastImageId,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        AddParameter(command, "$sessionId", sessionRecord.SessionId);
        AddParameter(command, "$orderId", sessionRecord.OrderId);
        AddParameter(command, "$patientId", sessionRecord.PatientId);
        AddParameter(command, "$patientName", sessionRecord.PatientName);
        AddParameter(command, "$procedureDescription", sessionRecord.ProcedureDescription);
        AddParameter(command, "$bodyPart", sessionRecord.BodyPart);
        AddParameter(command, "$projection", sessionRecord.Projection);
        AddParameter(command, "$workflowStatus", (int)sessionRecord.WorkflowStatus);
        AddParameter(command, "$deviceState", (int)sessionRecord.DeviceState);
        AddParameter(command, "$startedAtUtc", sessionRecord.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        AddParameter(command, "$lastExposureAtUtc", sessionRecord.LastExposureAtUtc?.ToString("O", CultureInfo.InvariantCulture));
        AddParameter(command, "$lastGeneratedArtifactPath", sessionRecord.LastGeneratedArtifactPath);
        AddParameter(command, "$lastImageId", sessionRecord.LastImageId);
        AddParameter(command, "$updatedAtUtc", sessionRecord.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public ExamSessionRecord? GetBySessionId(string sessionId)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, OrderId, PatientId, PatientName, ProcedureDescription, BodyPart, Projection,
                   WorkflowStatus, DeviceState, StartedAtUtc, LastExposureAtUtc, LastGeneratedArtifactPath,
                   LastImageId, UpdatedAtUtc
            FROM ExamSessions
            WHERE SessionId = $sessionId
            LIMIT 1;
            """;
        AddParameter(command, "$sessionId", sessionId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ExamSessionRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            (ExamWorkflowStatus)reader.GetInt32(7),
            (DeviceOperationalState)reader.GetInt32(8),
            ParseUtc(reader.GetString(9)),
            reader.IsDBNull(10) ? null : ParseUtc(reader.GetString(10)),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            ParseUtc(reader.GetString(13)));
    }

    public IReadOnlyList<ExamSessionRecord> GetRecent(int limit)
    {
        var sessions = new List<ExamSessionRecord>();

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SessionId, OrderId, PatientId, PatientName, ProcedureDescription, BodyPart, Projection,
                   WorkflowStatus, DeviceState, StartedAtUtc, LastExposureAtUtc, LastGeneratedArtifactPath,
                   LastImageId, UpdatedAtUtc
            FROM ExamSessions
            ORDER BY UpdatedAtUtc DESC
            LIMIT $limit;
            """;
        AddParameter(command, "$limit", limit);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(new ExamSessionRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                (ExamWorkflowStatus)reader.GetInt32(7),
                (DeviceOperationalState)reader.GetInt32(8),
                ParseUtc(reader.GetString(9)),
                reader.IsDBNull(10) ? null : ParseUtc(reader.GetString(10)),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                ParseUtc(reader.GetString(13))));
        }

        return sessions;
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}