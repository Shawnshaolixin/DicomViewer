using DicomViewer.WorkflowScpDemo;

namespace DicomViewer.Tests.WorkflowScpDemo;

public sealed class DiscoveryAuditStoreTests
{
    [Fact]
    public async Task PersistBatchAsync_UpsertsDeviceAndKeepsFirstSeen()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var dbPath = Path.Combine(tempDir.FullName, "discovery.db");
        var store = new DiscoveryAuditStore(dbPath);

        await store.InitializeAsync(CancellationToken.None);

        var firstSeen = DateTime.UtcNow.AddMinutes(-5);
        var secondSeen = DateTime.UtcNow;
        var deviceKey = DeviceDiscoveryAuditPipeline.BuildDeviceKey("127.0.0.1", "SCU_A", "RIS_SCP");

        await store.PersistBatchAsync(
            [new DiscoveredDeviceSnapshot
            {
                DeviceKey = deviceKey,
                RemoteIp = "127.0.0.1",
                RemotePort = 21112,
                CallingAeTitle = "SCU_A",
                CalledAeTitle = "RIS_SCP",
                FirstSeenUtc = firstSeen,
                LastSeenUtc = firstSeen,
            }],
            [new DicomProtocolAuditEvent("AssociationRequested", firstSeen, deviceKey, "conn-1", "127.0.0.1", 21112, "SCU_A", "RIS_SCP", "Requested", null)],
            CancellationToken.None);

        await store.PersistBatchAsync(
            [new DiscoveredDeviceSnapshot
            {
                DeviceKey = deviceKey,
                RemoteIp = "127.0.0.1",
                RemotePort = 21113,
                CallingAeTitle = "SCU_A",
                CalledAeTitle = "RIS_SCP",
                FirstSeenUtc = firstSeen,
                LastSeenUtc = secondSeen,
            }],
            [new DicomProtocolAuditEvent("AssociationAccepted", secondSeen, deviceKey, "conn-2", "127.0.0.1", 21113, "SCU_A", "RIS_SCP", "Accepted", null)],
            CancellationToken.None);

        var devices = await store.ListDevicesAsync(null, CancellationToken.None);

        var device = Assert.Single(devices);
        Assert.Equal(deviceKey, device.DeviceKey);
        Assert.Equal(firstSeen.ToString("O"), device.FirstSeenUtc.ToString("O"));
        Assert.Equal(secondSeen.ToString("O"), device.LastSeenUtc.ToString("O"));
        Assert.Equal(21113, device.RemotePort);

        var audits = await store.ListAuditLogsAsync(new AuditLogQuery { Take = 10 }, CancellationToken.None);
        Assert.Equal(2, audits.Count);
    }

    [Fact]
    public async Task UpdateDeviceMetadataAsync_UpdatesDeviceAndOptions()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var dbPath = Path.Combine(tempDir.FullName, "discovery.db");
        var store = new DiscoveryAuditStore(dbPath);

        await store.InitializeAsync(CancellationToken.None);

        var deviceKey = DeviceDiscoveryAuditPipeline.BuildDeviceKey("10.10.10.5", "MODALITY_A", "RIS_SCP");
        await store.PersistBatchAsync(
            [new DiscoveredDeviceSnapshot
            {
                DeviceKey = deviceKey,
                RemoteIp = "10.10.10.5",
                RemotePort = 104,
                CallingAeTitle = "MODALITY_A",
                CalledAeTitle = "RIS_SCP",
                FirstSeenUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow,
            }],
            [],
            CancellationToken.None);

        var device = Assert.Single(await store.ListDevicesAsync(null, CancellationToken.None));
        var updated = await store.UpdateDeviceMetadataAsync(
            device.Id,
            new DeviceMetadataUpdateRequest
            {
                DisplayName = "Room A DR",
                Remark = "Night shift",
                IsEnabled = false,
            },
            CancellationToken.None);

        Assert.True(updated);

        var devices = await store.ListDevicesAsync(null, CancellationToken.None);
        var refreshed = Assert.Single(devices);
        Assert.Equal("Room A DR", refreshed.DisplayName);
        Assert.Equal("Night shift", refreshed.Remark);
        Assert.False(refreshed.IsEnabled);

        var options = await store.ListDeviceOptionsAsync(CancellationToken.None);
        var option = Assert.Single(options);
        Assert.Equal("Room A DR", option.Label);
        Assert.False(option.IsEnabled);
    }
}
