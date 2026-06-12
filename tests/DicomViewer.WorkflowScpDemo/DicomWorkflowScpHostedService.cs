using System.Threading;
using Microsoft.Extensions.Hosting;

namespace DicomViewer.WorkflowScpDemo;

internal sealed class DicomWorkflowScpHostedService : IHostedService, IDisposable
{
    private readonly DicomWorkflowScpServer _server;
    private int _disposed;

    public DicomWorkflowScpHostedService(DicomWorkflowScpServer server)
    {
        _server = server;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _server.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _server.Dispose();
    }
}
