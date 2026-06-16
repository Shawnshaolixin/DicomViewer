namespace DicomViewer.WorkflowScpDemo;

internal sealed record DicomProtocolAuditEvent(
    string EventType,
    DateTime TimestampUtc,
    string DeviceKey,
    string ConnectionId,
    string RemoteIp,
    int? RemotePort,
    string CallingAeTitle,
    string CalledAeTitle,
    string Status,
    string? Detail);

internal sealed class DiscoveredDeviceSnapshot
{
    public required string DeviceKey { get; init; }

    public required string RemoteIp { get; init; }

    public int? RemotePort { get; set; }

    public string CallingAeTitle { get; set; } = string.Empty;

    public string CalledAeTitle { get; set; } = string.Empty;

    public DateTime FirstSeenUtc { get; set; }

    public DateTime LastSeenUtc { get; set; }

    public string? DisplayName { get; set; }

    public string? Remark { get; set; }

    public bool IsEnabled { get; set; } = true;
}

internal sealed class DeviceCatalogEntry
{
    public long Id { get; init; }

    public string DeviceKey { get; init; } = string.Empty;

    public string RemoteIp { get; init; } = string.Empty;

    public int? RemotePort { get; init; }

    public string CallingAeTitle { get; init; } = string.Empty;

    public string CalledAeTitle { get; init; } = string.Empty;

    public DateTime FirstSeenUtc { get; init; }

    public DateTime LastSeenUtc { get; init; }

    public string? DisplayName { get; init; }

    public string? Remark { get; init; }

    public bool IsEnabled { get; init; }
}

internal sealed class DeviceOptionItem
{
    public long Id { get; init; }

    public string Label { get; init; } = string.Empty;

    public string CallingAeTitle { get; init; } = string.Empty;

    public string RemoteIp { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}

internal sealed class DeviceMetadataUpdateRequest
{
    public string? DisplayName { get; init; }

    public string? Remark { get; init; }

    public bool? IsEnabled { get; init; }
}

internal sealed class AuditLogEntry
{
    public long Id { get; init; }

    public DateTime TimestampUtc { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string DeviceKey { get; init; } = string.Empty;

    public string ConnectionId { get; init; } = string.Empty;

    public string RemoteIp { get; init; } = string.Empty;

    public int? RemotePort { get; init; }

    public string CallingAeTitle { get; init; } = string.Empty;

    public string CalledAeTitle { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Detail { get; init; }
}

internal sealed class AuditLogQuery
{
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string? EventType { get; init; }

    public string? DeviceKey { get; init; }

    public int Skip { get; init; }

    public int Take { get; init; } = 200;
}
