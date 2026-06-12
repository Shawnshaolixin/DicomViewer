using DicomViewer.WorkflowScpDemo;

var options = WorkflowServerOptions.Parse(args);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(options.HttpUrl);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(_ => WorkflowDataStore.Create(options.DataFilePath));
builder.Services.AddSingleton(_ => new DiscoveryAuditStore(options.DatabaseFilePath));
builder.Services.AddSingleton<DeviceDiscoveryAuditPipeline>();
builder.Services.AddSingleton<IWorkflowScpAuditSink>(sp => sp.GetRequiredService<DeviceDiscoveryAuditPipeline>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DeviceDiscoveryAuditPipeline>());
builder.Services.AddSingleton<DicomWorkflowScpServer>();
builder.Services.AddHostedService<DicomWorkflowScpHostedService>();

var app = builder.Build();

app.MapGet("/api/devices", async (bool? enabledOnly, DeviceDiscoveryAuditPipeline pipeline, CancellationToken cancellationToken) =>
{
    return Results.Ok(await pipeline.ListDevicesAsync(enabledOnly, cancellationToken));
});

app.MapGet("/api/device-options", async (DeviceDiscoveryAuditPipeline pipeline, CancellationToken cancellationToken) =>
{
    return Results.Ok(await pipeline.ListDeviceOptionsAsync(cancellationToken));
});

app.MapPatch("/api/devices/{id:long}", async (long id, DeviceMetadataUpdateRequest request, DeviceDiscoveryAuditPipeline pipeline, CancellationToken cancellationToken) =>
{
    if (request.DisplayName is null && request.Remark is null && request.IsEnabled is null)
    {
        return Results.BadRequest("At least one field must be provided for update.");
    }

    var updated = await pipeline.UpdateDeviceMetadataAsync(id, request, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/audit-logs", async (
    DateTime? fromUtc,
    DateTime? toUtc,
    string? eventType,
    string? deviceKey,
    int? skip,
    int? take,
    DeviceDiscoveryAuditPipeline pipeline,
    CancellationToken cancellationToken) =>
{
    var query = new AuditLogQuery
    {
        FromUtc = fromUtc,
        ToUtc = toUtc,
        EventType = eventType,
        DeviceKey = deviceKey,
        Skip = skip ?? 0,
        Take = take ?? 200,
    };

    return Results.Ok(await pipeline.ListAuditLogsAsync(query, cancellationToken));
});

app.MapGet("/", () => Results.Ok(new
{
    Message = "Workflow SCP Demo WebAPI is running.",
    options.Host,
    options.Port,
    options.CalledAeTitle,
    options.DataFilePath,
    options.DatabaseFilePath,
    options.HttpUrl,
}));

Console.WriteLine("MWL/MPPS SCP + WebAPI Demo 已启动。");
Console.WriteLine($"DICOM SCP 监听: {options.Host}:{options.Port}");
Console.WriteLine($"DICOM AE Title: {options.CalledAeTitle}");
Console.WriteLine($"Workflow 数据文件: {options.DataFilePath}");
Console.WriteLine($"设备目录/Audit 数据库: {options.DatabaseFilePath}");
Console.WriteLine($"WebAPI: {options.HttpUrl}");

await app.RunAsync();
