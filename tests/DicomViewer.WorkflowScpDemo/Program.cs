using DicomViewer.WorkflowScpDemo;

var options = WorkflowServerOptions.Parse(args);
var store = WorkflowDataStore.Create(options.DataFilePath);
using var server = new DicomWorkflowScpServer(store, options);

Console.WriteLine("MWL/MPPS SCP 已启动。");
Console.WriteLine($"监听: {options.Host}:{options.Port}");
Console.WriteLine($"AE Title: {options.CalledAeTitle}");
Console.WriteLine($"数据文件: {options.DataFilePath}");
Console.WriteLine("按 Ctrl+C 退出。");

using var shutdown = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Set();
};

server.Start();
shutdown.Wait();
