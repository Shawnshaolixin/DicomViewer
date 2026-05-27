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
                LastImageId, MppsInstanceUid, MppsStatus, MppsCreatedAtUtc, MppsLastSentAtUtc,
                MppsLastError, ScheduledProcedureStepIdSnapshot, AccessionNumberSnapshot, UpdatedAtUtc)
            VALUES (
                $sessionId, $orderId, $patientId, $patientName, $procedureDescription, $bodyPart, $projection,
                $workflowStatus, $deviceState, $startedAtUtc, $lastExposureAtUtc, $lastGeneratedArtifactPath,
                $lastImageId, $mppsInstanceUid, $mppsStatus, $mppsCreatedAtUtc, $mppsLastSentAtUtc,
                $mppsLastError, $scheduledProcedureStepIdSnapshot, $accessionNumberSnapshot, $updatedAtUtc)
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
                MppsInstanceUid = excluded.MppsInstanceUid,
                MppsStatus = excluded.MppsStatus,
                MppsCreatedAtUtc = excluded.MppsCreatedAtUtc,
                MppsLastSentAtUtc = excluded.MppsLastSentAtUtc,
                MppsLastError = excluded.MppsLastError,
                ScheduledProcedureStepIdSnapshot = excluded.ScheduledProcedureStepIdSnapshot,
                AccessionNumberSnapshot = excluded.AccessionNumberSnapshot,
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
        AddParameter(command, "$mppsInstanceUid", sessionRecord.MppsInstanceUid);
        AddParameter(command, "$mppsStatus", (int)sessionRecord.MppsStatus);
        AddParameter(command, "$mppsCreatedAtUtc", sessionRecord.MppsCreatedAtUtc?.ToString("O", CultureInfo.InvariantCulture));
        AddParameter(command, "$mppsLastSentAtUtc", sessionRecord.MppsLastSentAtUtc?.ToString("O", CultureInfo.InvariantCulture));
        AddParameter(command, "$mppsLastError", sessionRecord.MppsLastError);
        AddParameter(command, "$scheduledProcedureStepIdSnapshot", sessionRecord.ScheduledProcedureStepIdSnapshot);
        AddParameter(command, "$accessionNumberSnapshot", sessionRecord.AccessionNumberSnapshot);
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
                 LastImageId, UpdatedAtUtc, MppsInstanceUid, MppsStatus, MppsCreatedAtUtc,
                 MppsLastSentAtUtc, MppsLastError, ScheduledProcedureStepIdSnapshot, AccessionNumberSnapshot
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
            ParseUtc(reader.GetString(13)),
            reader.IsDBNull(14) ? null : reader.GetString(14),
            reader.IsDBNull(15) ? DicomViewer.Domain.Enums.MppsStatus.None : (DicomViewer.Domain.Enums.MppsStatus)reader.GetInt32(15),
            reader.IsDBNull(16) ? null : ParseUtc(reader.GetString(16)),
            reader.IsDBNull(17) ? null : ParseUtc(reader.GetString(17)),
            reader.IsDBNull(18) ? null : reader.GetString(18),
            reader.IsDBNull(19) ? null : reader.GetString(19),
            reader.IsDBNull(20) ? null : reader.GetString(20));
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
                 LastImageId, UpdatedAtUtc, MppsInstanceUid, MppsStatus, MppsCreatedAtUtc,
                 MppsLastSentAtUtc, MppsLastError, ScheduledProcedureStepIdSnapshot, AccessionNumberSnapshot
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
                ParseUtc(reader.GetString(13)),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? DicomViewer.Domain.Enums.MppsStatus.None : (DicomViewer.Domain.Enums.MppsStatus)reader.GetInt32(15),
                reader.IsDBNull(16) ? null : ParseUtc(reader.GetString(16)),
                reader.IsDBNull(17) ? null : ParseUtc(reader.GetString(17)),
                reader.IsDBNull(18) ? null : reader.GetString(18),
                reader.IsDBNull(19) ? null : reader.GetString(19),
                reader.IsDBNull(20) ? null : reader.GetString(20)));
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