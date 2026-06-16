using Microsoft.Data.Sqlite;

namespace DicomViewer.WorkflowScpDemo;

internal sealed class DiscoveryAuditStore
{
    private readonly string _connectionString;

    public DiscoveryAuditStore(string databaseFilePath)
    {
        var fullPath = Path.GetFullPath(databaseFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS device_catalog (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_key TEXT NOT NULL UNIQUE,
                remote_ip TEXT NOT NULL,
                remote_port INTEGER NULL,
                calling_ae_title TEXT NOT NULL,
                called_ae_title TEXT NOT NULL,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL,
                display_name TEXT NULL,
                remark TEXT NULL,
                is_enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS dicom_audit_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                event_type TEXT NOT NULL,
                device_key TEXT NOT NULL,
                connection_id TEXT NOT NULL,
                remote_ip TEXT NOT NULL,
                remote_port INTEGER NULL,
                calling_ae_title TEXT NOT NULL,
                called_ae_title TEXT NOT NULL,
                status TEXT NOT NULL,
                detail TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_dicom_audit_logs_timestamp ON dicom_audit_logs(timestamp_utc);
            CREATE INDEX IF NOT EXISTS idx_dicom_audit_logs_device_key ON dicom_audit_logs(device_key);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiscoveredDeviceSnapshot>> LoadDeviceCacheAsync(CancellationToken cancellationToken)
    {
        var results = new List<DiscoveredDeviceSnapshot>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                device_key,
                remote_ip,
                remote_port,
                calling_ae_title,
                called_ae_title,
                first_seen_utc,
                last_seen_utc,
                display_name,
                remark,
                is_enabled
            FROM device_catalog;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DiscoveredDeviceSnapshot
            {
                DeviceKey = reader.GetString(0),
                RemoteIp = reader.GetString(1),
                RemotePort = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                CallingAeTitle = reader.GetString(3),
                CalledAeTitle = reader.GetString(4),
                FirstSeenUtc = ParseUtc(reader.GetString(5)),
                LastSeenUtc = ParseUtc(reader.GetString(6)),
                DisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                Remark = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsEnabled = reader.GetInt32(9) == 1,
            });
        }

        return results;
    }

    public async Task PersistBatchAsync(
        IReadOnlyCollection<DiscoveredDeviceSnapshot> deviceUpdates,
        IReadOnlyCollection<DicomProtocolAuditEvent> auditEvents,
        CancellationToken cancellationToken)
    {
        if (deviceUpdates.Count == 0 && auditEvents.Count == 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var device in deviceUpdates)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO device_catalog (
                    device_key,
                    remote_ip,
                    remote_port,
                    calling_ae_title,
                    called_ae_title,
                    first_seen_utc,
                    last_seen_utc,
                    is_enabled
                )
                VALUES (
                    $device_key,
                    $remote_ip,
                    $remote_port,
                    $calling_ae_title,
                    $called_ae_title,
                    $first_seen_utc,
                    $last_seen_utc,
                    1
                )
                ON CONFLICT(device_key) DO UPDATE SET
                    remote_ip = excluded.remote_ip,
                    remote_port = excluded.remote_port,
                    calling_ae_title = excluded.calling_ae_title,
                    called_ae_title = excluded.called_ae_title,
                    last_seen_utc = excluded.last_seen_utc;
                """;
            command.Parameters.AddWithValue("$device_key", device.DeviceKey);
            command.Parameters.AddWithValue("$remote_ip", device.RemoteIp);
            command.Parameters.AddWithValue("$remote_port", device.RemotePort is null ? DBNull.Value : device.RemotePort.Value);
            command.Parameters.AddWithValue("$calling_ae_title", device.CallingAeTitle);
            command.Parameters.AddWithValue("$called_ae_title", device.CalledAeTitle);
            command.Parameters.AddWithValue("$first_seen_utc", device.FirstSeenUtc.ToString("O"));
            command.Parameters.AddWithValue("$last_seen_utc", device.LastSeenUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var auditEvent in auditEvents)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO dicom_audit_logs (
                    timestamp_utc,
                    event_type,
                    device_key,
                    connection_id,
                    remote_ip,
                    remote_port,
                    calling_ae_title,
                    called_ae_title,
                    status,
                    detail
                )
                VALUES (
                    $timestamp_utc,
                    $event_type,
                    $device_key,
                    $connection_id,
                    $remote_ip,
                    $remote_port,
                    $calling_ae_title,
                    $called_ae_title,
                    $status,
                    $detail
                );
                """;
            command.Parameters.AddWithValue("$timestamp_utc", auditEvent.TimestampUtc.ToString("O"));
            command.Parameters.AddWithValue("$event_type", auditEvent.EventType);
            command.Parameters.AddWithValue("$device_key", auditEvent.DeviceKey);
            command.Parameters.AddWithValue("$connection_id", auditEvent.ConnectionId);
            command.Parameters.AddWithValue("$remote_ip", auditEvent.RemoteIp);
            command.Parameters.AddWithValue("$remote_port", auditEvent.RemotePort is null ? DBNull.Value : auditEvent.RemotePort.Value);
            command.Parameters.AddWithValue("$calling_ae_title", auditEvent.CallingAeTitle);
            command.Parameters.AddWithValue("$called_ae_title", auditEvent.CalledAeTitle);
            command.Parameters.AddWithValue("$status", auditEvent.Status);
            command.Parameters.AddWithValue("$detail", auditEvent.Detail is null ? DBNull.Value : auditEvent.Detail);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceCatalogEntry>> ListDevicesAsync(bool? enabledOnly, CancellationToken cancellationToken)
    {
        var results = new List<DeviceCatalogEntry>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                device_key,
                remote_ip,
                remote_port,
                calling_ae_title,
                called_ae_title,
                first_seen_utc,
                last_seen_utc,
                display_name,
                remark,
                is_enabled
            FROM device_catalog
            WHERE ($enabled_only IS NULL OR is_enabled = $enabled_only)
            ORDER BY last_seen_utc DESC;
            """;
        command.Parameters.AddWithValue("$enabled_only", enabledOnly is null ? DBNull.Value : enabledOnly.Value ? 1 : 0);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DeviceCatalogEntry
            {
                Id = reader.GetInt64(0),
                DeviceKey = reader.GetString(1),
                RemoteIp = reader.GetString(2),
                RemotePort = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                CallingAeTitle = reader.GetString(4),
                CalledAeTitle = reader.GetString(5),
                FirstSeenUtc = ParseUtc(reader.GetString(6)),
                LastSeenUtc = ParseUtc(reader.GetString(7)),
                DisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                Remark = reader.IsDBNull(9) ? null : reader.GetString(9),
                IsEnabled = reader.GetInt32(10) == 1,
            });
        }

        return results;
    }

    public async Task<IReadOnlyList<DeviceOptionItem>> ListDeviceOptionsAsync(CancellationToken cancellationToken)
    {
        var results = new List<DeviceOptionItem>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                COALESCE(NULLIF(display_name, ''), calling_ae_title || ' @ ' || remote_ip) AS label,
                calling_ae_title,
                remote_ip,
                is_enabled
            FROM device_catalog
            ORDER BY is_enabled DESC, label ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DeviceOptionItem
            {
                Id = reader.GetInt64(0),
                Label = reader.GetString(1),
                CallingAeTitle = reader.GetString(2),
                RemoteIp = reader.GetString(3),
                IsEnabled = reader.GetInt32(4) == 1,
            });
        }

        return results;
    }

    public async Task<bool> UpdateDeviceMetadataAsync(long id, DeviceMetadataUpdateRequest update, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE device_catalog
            SET
                display_name = CASE WHEN $display_name_set = 1 THEN $display_name ELSE display_name END,
                remark = CASE WHEN $remark_set = 1 THEN $remark ELSE remark END,
                is_enabled = CASE WHEN $is_enabled_set = 1 THEN $is_enabled ELSE is_enabled END
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$display_name_set", update.DisplayName is null ? 0 : 1);
        command.Parameters.AddWithValue("$display_name", update.DisplayName is null ? DBNull.Value : update.DisplayName.Trim());
        command.Parameters.AddWithValue("$remark_set", update.Remark is null ? 0 : 1);
        command.Parameters.AddWithValue("$remark", update.Remark is null ? DBNull.Value : update.Remark.Trim());
        command.Parameters.AddWithValue("$is_enabled_set", update.IsEnabled is null ? 0 : 1);
        command.Parameters.AddWithValue("$is_enabled", update.IsEnabled.HasValue ? (update.IsEnabled.Value ? 1 : 0) : 0);
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(query.Take, 1, 1000);
        var safeSkip = Math.Max(0, query.Skip);

        var results = new List<AuditLogEntry>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                timestamp_utc,
                event_type,
                device_key,
                connection_id,
                remote_ip,
                remote_port,
                calling_ae_title,
                called_ae_title,
                status,
                detail
            FROM dicom_audit_logs
            WHERE
                ($from_utc IS NULL OR timestamp_utc >= $from_utc)
                AND ($to_utc IS NULL OR timestamp_utc <= $to_utc)
                AND ($event_type IS NULL OR event_type = $event_type)
                AND ($device_key IS NULL OR device_key = $device_key)
            ORDER BY timestamp_utc DESC
            LIMIT $take OFFSET $skip;
            """;
        command.Parameters.AddWithValue("$from_utc", query.FromUtc is null ? DBNull.Value : query.FromUtc.Value.ToString("O"));
        command.Parameters.AddWithValue("$to_utc", query.ToUtc is null ? DBNull.Value : query.ToUtc.Value.ToString("O"));
        command.Parameters.AddWithValue("$event_type", string.IsNullOrWhiteSpace(query.EventType) ? DBNull.Value : query.EventType.Trim());
        command.Parameters.AddWithValue("$device_key", string.IsNullOrWhiteSpace(query.DeviceKey) ? DBNull.Value : query.DeviceKey.Trim());
        command.Parameters.AddWithValue("$take", safeTake);
        command.Parameters.AddWithValue("$skip", safeSkip);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AuditLogEntry
            {
                Id = reader.GetInt64(0),
                TimestampUtc = ParseUtc(reader.GetString(1)),
                EventType = reader.GetString(2),
                DeviceKey = reader.GetString(3),
                ConnectionId = reader.GetString(4),
                RemoteIp = reader.GetString(5),
                RemotePort = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                CallingAeTitle = reader.GetString(7),
                CalledAeTitle = reader.GetString(8),
                Status = reader.GetString(9),
                Detail = reader.IsDBNull(10) ? null : reader.GetString(10),
            });
        }

        return results;
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}
