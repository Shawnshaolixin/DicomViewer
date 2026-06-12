using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace DicomViewer.WorkflowScpDemo;

internal interface IWorkflowScpAuditSink
{
    void RecordEvent(DicomProtocolAuditEvent auditEvent);
}

internal sealed class DeviceDiscoveryAuditPipeline : BackgroundService, IWorkflowScpAuditSink
{
    private const int BatchSize = 64;

    private readonly DiscoveryAuditStore _store;
    private readonly Channel<DicomProtocolAuditEvent> _channel;
    private ConcurrentDictionary<string, DiscoveredDeviceSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    public DeviceDiscoveryAuditPipeline(DiscoveryAuditStore store)
    {
        _store = store;
        _channel = Channel.CreateUnbounded<DicomProtocolAuditEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public void RecordEvent(DicomProtocolAuditEvent auditEvent)
    {
        var normalized = Normalize(auditEvent);
        var cache = _cache;
        cache.AddOrUpdate(
            normalized.DeviceKey,
            _ => new DiscoveredDeviceSnapshot
            {
                DeviceKey = normalized.DeviceKey,
                RemoteIp = normalized.RemoteIp,
                RemotePort = normalized.RemotePort,
                CallingAeTitle = normalized.CallingAeTitle,
                CalledAeTitle = normalized.CalledAeTitle,
                FirstSeenUtc = normalized.TimestampUtc,
                LastSeenUtc = normalized.TimestampUtc,
            },
            (_, existing) =>
            {
                existing.RemotePort = normalized.RemotePort;
                existing.CallingAeTitle = normalized.CallingAeTitle;
                existing.CalledAeTitle = normalized.CalledAeTitle;
                existing.LastSeenUtc = normalized.TimestampUtc;
                return existing;
            });

        _channel.Writer.TryWrite(normalized);
    }

    public Task<IReadOnlyList<DeviceCatalogEntry>> ListDevicesAsync(bool? enabledOnly, CancellationToken cancellationToken)
    {
        return _store.ListDevicesAsync(enabledOnly, cancellationToken);
    }

    public Task<IReadOnlyList<DeviceOptionItem>> ListDeviceOptionsAsync(CancellationToken cancellationToken)
    {
        return _store.ListDeviceOptionsAsync(cancellationToken);
    }

    public async Task<bool> UpdateDeviceMetadataAsync(long id, DeviceMetadataUpdateRequest request, CancellationToken cancellationToken)
    {
        var updated = await _store.UpdateDeviceMetadataAsync(id, request, cancellationToken);
        if (!updated)
        {
            return false;
        }

        var devices = await _store.ListDevicesAsync(null, cancellationToken);
        var replacement = new ConcurrentDictionary<string, DiscoveredDeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in devices)
        {
            replacement[item.DeviceKey] = new DiscoveredDeviceSnapshot
            {
                DeviceKey = item.DeviceKey,
                RemoteIp = item.RemoteIp,
                RemotePort = item.RemotePort,
                CallingAeTitle = item.CallingAeTitle,
                CalledAeTitle = item.CalledAeTitle,
                FirstSeenUtc = item.FirstSeenUtc,
                LastSeenUtc = item.LastSeenUtc,
                DisplayName = item.DisplayName,
                Remark = item.Remark,
                IsEnabled = item.IsEnabled,
            };
        }

        Interlocked.Exchange(ref _cache, replacement);
        return true;
    }

    public Task<IReadOnlyList<AuditLogEntry>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        return _store.ListAuditLogsAsync(query, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _store.InitializeAsync(stoppingToken);
        var existing = await _store.LoadDeviceCacheAsync(stoppingToken);
        var seed = new ConcurrentDictionary<string, DiscoveredDeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in existing)
        {
            seed[item.DeviceKey] = item;
        }

        Interlocked.Exchange(ref _cache, seed);

        var pending = new List<DicomProtocolAuditEvent>(BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            while (pending.Count < BatchSize && _channel.Reader.TryRead(out var immediate))
            {
                pending.Add(immediate);
            }

            if (pending.Count == 0)
            {
                if (!await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    break;
                }

                continue;
            }

            await FlushPendingAsync(pending, stoppingToken);
            pending.Clear();
        }

        while (_channel.Reader.TryRead(out var remaining))
        {
            pending.Add(remaining);
            if (pending.Count >= BatchSize)
            {
                await FlushPendingAsync(pending, CancellationToken.None);
                pending.Clear();
            }
        }

        if (pending.Count > 0)
        {
            await FlushPendingAsync(pending, CancellationToken.None);
        }
    }

    private async Task FlushPendingAsync(IReadOnlyCollection<DicomProtocolAuditEvent> pending, CancellationToken cancellationToken)
    {
        var latestByDevice = new Dictionary<string, DiscoveredDeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
        var cache = _cache;
        foreach (var auditEvent in pending)
        {
            if (cache.TryGetValue(auditEvent.DeviceKey, out var cached))
            {
                latestByDevice[auditEvent.DeviceKey] = new DiscoveredDeviceSnapshot
                {
                    DeviceKey = cached.DeviceKey,
                    RemoteIp = cached.RemoteIp,
                    RemotePort = cached.RemotePort,
                    CallingAeTitle = cached.CallingAeTitle,
                    CalledAeTitle = cached.CalledAeTitle,
                    FirstSeenUtc = cached.FirstSeenUtc,
                    LastSeenUtc = cached.LastSeenUtc,
                    DisplayName = cached.DisplayName,
                    Remark = cached.Remark,
                    IsEnabled = cached.IsEnabled,
                };
            }
        }

        await _store.PersistBatchAsync(latestByDevice.Values.ToArray(), pending, cancellationToken);
    }

    private static DicomProtocolAuditEvent Normalize(DicomProtocolAuditEvent source)
    {
        var remoteIp = string.IsNullOrWhiteSpace(source.RemoteIp) ? "unknown" : source.RemoteIp.Trim();
        var calling = string.IsNullOrWhiteSpace(source.CallingAeTitle) ? "UNKNOWN" : source.CallingAeTitle.Trim();
        var called = string.IsNullOrWhiteSpace(source.CalledAeTitle) ? "UNKNOWN" : source.CalledAeTitle.Trim();
        var key = !string.IsNullOrWhiteSpace(source.DeviceKey)
            ? source.DeviceKey
            : BuildDeviceKey(remoteIp, calling, called);

        return source with
        {
            TimestampUtc = source.TimestampUtc == default ? DateTime.UtcNow : source.TimestampUtc,
            DeviceKey = key,
            RemoteIp = remoteIp,
            CallingAeTitle = calling,
            CalledAeTitle = called,
            Status = string.IsNullOrWhiteSpace(source.Status) ? "Unknown" : source.Status.Trim(),
            ConnectionId = string.IsNullOrWhiteSpace(source.ConnectionId) ? Guid.NewGuid().ToString("N") : source.ConnectionId,
            Detail = string.IsNullOrWhiteSpace(source.Detail) ? null : source.Detail.Trim(),
        };
    }

    public static string BuildDeviceKey(string remoteIp, string callingAeTitle, string calledAeTitle)
    {
        var safeIp = string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp.Trim().ToUpperInvariant();
        var safeCalling = string.IsNullOrWhiteSpace(callingAeTitle) ? "UNKNOWN" : callingAeTitle.Trim().ToUpperInvariant();
        var safeCalled = string.IsNullOrWhiteSpace(calledAeTitle) ? "UNKNOWN" : calledAeTitle.Trim().ToUpperInvariant();
        return $"{safeCalling}|{safeCalled}|{safeIp}";
    }
}
