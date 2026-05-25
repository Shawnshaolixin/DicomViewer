using System.Text;
using DicomViewer.Application.Models;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomViewer.Infrastructure.Pacs;

public sealed class LocalDicomStoreScpService : ILocalDicomStoreScpService, IDisposable
{
    private readonly object _gate = new();
    private IDicomServer? _server;
    private string _listeningHost = string.Empty;
    private int _listeningPort;
    private string _expectedAeTitle = PacsConfiguration.Default.CallingAeTitle;
    private LocalDicomReceiveSession? _activeSession;

    public LocalDicomReceiveSession PrepareReceive(PacsConfiguration configuration, string targetDirectory)
    {
        lock (_gate)
        {
            EnsureServer(configuration);
            Directory.CreateDirectory(targetDirectory);
            _expectedAeTitle = configuration.CallingAeTitle;
            _activeSession = new LocalDicomReceiveSession(targetDirectory);
            return _activeSession;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _server?.Stop();
            _server = null;
        }
    }

    internal string? SaveIncomingFile(DicomCStoreRequest request)
    {
        lock (_gate)
        {
            if (_activeSession is null)
            {
                return null;
            }

            var fileName = SanitizeFileName(request.SOPInstanceUID?.UID ?? Guid.NewGuid().ToString("N"));
            var filePath = Path.Combine(_activeSession.TargetDirectory, $"{fileName}.dcm");
            request.File.Save(filePath);
            _activeSession.AddReceivedFile(filePath);
            return filePath;
        }
    }

    internal bool AcceptsCalledAe(string? calledAe)
    {
        lock (_gate)
        {
            return string.Equals(calledAe, _expectedAeTitle, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void EnsureServer(PacsConfiguration configuration)
    {
        var host = string.IsNullOrWhiteSpace(configuration.LocalStoreHost) ? "127.0.0.1" : configuration.LocalStoreHost.Trim();
        if (_server is not null && string.Equals(_listeningHost, host, StringComparison.OrdinalIgnoreCase) && _listeningPort == configuration.LocalStorePort)
        {
            return;
        }

        _server?.Stop();
        _listeningHost = host;
        _listeningPort = configuration.LocalStorePort;
        _server = DicomServerFactory.Create<LocalDicomStoreScp>(
            _listeningHost,
            _listeningPort,
            null,
            Encoding.UTF8,
            NullLogger.Instance,
            this,
            null);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private sealed class LocalDicomStoreScp : DicomService, IDicomServiceProvider, IDicomCStoreProvider
    {
        public LocalDicomStoreScp(INetworkStream stream, Encoding fallbackEncoding, Microsoft.Extensions.Logging.ILogger logger, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, logger, dependencies)
        {
        }

        public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            if (UserState is not LocalDicomStoreScpService service || !service.AcceptsCalledAe(association.CalledAE))
            {
                await SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
                return;
            }

            foreach (var context in association.PresentationContexts)
            {
                context.AcceptTransferSyntaxes([
                    DicomTransferSyntax.ImplicitVRLittleEndian,
                    DicomTransferSyntax.ExplicitVRLittleEndian,
                    DicomTransferSyntax.ExplicitVRBigEndian,
                ]);
            }

            await SendAssociationAcceptAsync(association);
        }

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            return SendAssociationReleaseResponseAsync();
        }

        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
        }

        public void OnConnectionClosed(Exception? exception)
        {
        }

        public Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
        {
            if (UserState is not LocalDicomStoreScpService service)
            {
                return Task.FromResult(new DicomCStoreResponse(request, DicomStatus.ProcessingFailure));
            }

            var filePath = service.SaveIncomingFile(request);
            return Task.FromResult(new DicomCStoreResponse(request, filePath is null ? DicomStatus.ProcessingFailure : DicomStatus.Success));
        }

        public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
        {
            return Task.CompletedTask;
        }
    }
}