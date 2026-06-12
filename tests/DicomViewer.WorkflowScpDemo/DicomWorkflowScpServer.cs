using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomViewer.WorkflowScpDemo;

internal sealed class DicomWorkflowScpServer : IDisposable
{
    private readonly WorkflowScpState _state;
    private IDicomServer? _server;

    public DicomWorkflowScpServer(
        WorkflowDataStore store,
        WorkflowServerOptions options,
        IWorkflowScpAuditSink auditSink,
        ILogger<DicomWorkflowScpServer>? logger = null)
    {
        _state = new WorkflowScpState(store, options.CalledAeTitle, auditSink, logger ?? NullLogger<DicomWorkflowScpServer>.Instance);
        Options = options;
    }

    public WorkflowServerOptions Options { get; }

    public void Start()
    {
        _server = DicomServerFactory.Create<WorkflowScpService>(
            Options.Host,
            Options.Port,
            null,
            Encoding.UTF8,
            NullLogger.Instance,
            _state,
            null);
    }

    public void Dispose()
    {
        _server?.Stop();
    }

    private sealed record WorkflowScpState(
        WorkflowDataStore Store,
        string CalledAeTitle,
        IWorkflowScpAuditSink AuditSink,
        ILogger ServerLogger);

    private sealed class WorkflowScpService : DicomService, IDicomServiceProvider, IDicomCFindProvider, IDicomNServiceProvider
    {
        private readonly string _connectionId = Guid.NewGuid().ToString("N");
        private readonly string _remoteIp;
        private readonly int? _remotePort;
        private string _callingAeTitle = "UNKNOWN";
        private string _calledAeTitle = "UNKNOWN";

        public WorkflowScpService(INetworkStream stream, Encoding fallbackEncoding, ILogger logger, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, logger, dependencies)
        {
            (_remoteIp, _remotePort) = TryGetRemoteEndpoint(stream, logger);
        }

        public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            if (UserState is not WorkflowScpState state)
            {
                await SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceProviderACSE, DicomRejectReason.NoReasonGiven);
                return;
            }

            _callingAeTitle = NormalizeAe(association.CallingAE);
            _calledAeTitle = NormalizeAe(association.CalledAE);

            ReportAudit(state, "AssociationRequested", "Requested", null);
            state.ServerLogger.LogInformation(
                "Association requested from {CallingAE} to {CalledAE}, remote={RemoteIp}:{RemotePort}",
                _callingAeTitle,
                _calledAeTitle,
                _remoteIp,
                _remotePort);

            if (!string.Equals(state.CalledAeTitle, association.CalledAE, StringComparison.OrdinalIgnoreCase))
            {
                ReportAudit(state, "AssociationRejected", "CalledAENotRecognized", null);
                await SendAssociationRejectAsync(DicomRejectResult.Permanent, DicomRejectSource.ServiceUser, DicomRejectReason.CalledAENotRecognized);
                return;
            }

            foreach (var context in association.PresentationContexts)
            {
                if (context.AbstractSyntax == DicomUID.ModalityWorklistInformationModelFind || context.AbstractSyntax == DicomUID.ModalityPerformedProcedureStep)
                {
                    context.AcceptTransferSyntaxes([DicomTransferSyntax.ImplicitVRLittleEndian, DicomTransferSyntax.ExplicitVRLittleEndian]);
                }
            }

            ReportAudit(state, "AssociationAccepted", "Accepted", null);
            await SendAssociationAcceptAsync(association);
        }

        public Task OnReceiveAssociationReleaseRequestAsync()
        {
            if (UserState is WorkflowScpState state)
            {
                ReportAudit(state, "AssociationReleaseRequested", "ReleaseRequested", null);
            }

            return SendAssociationReleaseResponseAsync();
        }

        public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
        {
            if (UserState is WorkflowScpState state)
            {
                ReportAudit(state, "AssociationAborted", source.ToString(), reason.ToString());
            }
        }

        public void OnConnectionClosed(Exception? exception)
        {
            if (UserState is WorkflowScpState state)
            {
                var detail = exception is null ? null : exception.GetType().Name;
                ReportAudit(state, "ConnectionClosed", exception is null ? "Closed" : "ClosedWithError", detail);
            }
        }

        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {
            if (UserState is not WorkflowScpState state)
            {
                yield return new DicomCFindResponse(request, DicomStatus.ProcessingFailure);
                yield break;
            }

            var criteria = ReadQueryCriteria(request.Dataset);
            ReportAudit(
                state,
                "MwlCFind",
                "Requested",
                $"PatientId={criteria.PatientId};AccessionNumber={criteria.AccessionNumber};Modality={criteria.Modality}");

            foreach (var item in state.Store.QueryWorklist(criteria))
            {
                yield return new DicomCFindResponse(request, DicomStatus.Pending)
                {
                    Dataset = BuildWorklistDataset(item),
                };
            }

            ReportAudit(state, "MwlCFind", "Completed", null);
            yield return new DicomCFindResponse(request, DicomStatus.Success);
            await Task.CompletedTask;
        }

        public Task<DicomNCreateResponse> OnNCreateRequestAsync(DicomNCreateRequest request)
        {
            if (UserState is not WorkflowScpState state)
            {
                return Task.FromResult(new DicomNCreateResponse(request, DicomStatus.ProcessingFailure));
            }

            var dataset = request.Dataset ?? new DicomDataset();
            var scheduledAttributes = TryGetFirstSequenceItem(dataset, DicomTag.ScheduledStepAttributesSequence);
            var result = state.Store.CreateMpps(
                request.SOPInstanceUID?.UID ?? string.Empty,
                GetString(dataset, DicomTag.PerformedProcedureStepID),
                GetString(scheduledAttributes, DicomTag.ScheduledProcedureStepID),
                GetString(dataset, DicomTag.AccessionNumber),
                GetString(dataset, DicomTag.StudyInstanceUID),
                GetString(dataset, DicomTag.PatientID),
                GetString(dataset, DicomTag.PatientName));

            var response = new DicomNCreateResponse(
                request,
                result.Success
                    ? DicomStatus.Success
                    : string.Equals(result.Message, "SOP Instance UID 已存在。", StringComparison.Ordinal)
                        ? DicomStatus.DuplicateSOPInstance
                        : DicomStatus.NoSuchObjectInstance)
            {
                Dataset = BuildMppsEchoDataset(result.Record, dataset),
            };

            ReportAudit(
                state,
                "MppsNCreate",
                result.Success ? "Success" : "Failed",
                $"SopInstanceUid={request.SOPInstanceUID?.UID};PerformedProcedureStepId={GetString(dataset, DicomTag.PerformedProcedureStepID)}");

            return Task.FromResult(response);
        }

        public Task<DicomNSetResponse> OnNSetRequestAsync(DicomNSetRequest request)
        {
            if (UserState is not WorkflowScpState state)
            {
                return Task.FromResult(new DicomNSetResponse(request, DicomStatus.ProcessingFailure));
            }

            var dataset = request.Dataset ?? new DicomDataset();
            var result = state.Store.UpdateMpps(
                request.SOPInstanceUID?.UID ?? string.Empty,
                GetString(dataset, DicomTag.PerformedProcedureStepStatus),
                GetString(dataset, DicomTag.CommentsOnThePerformedProcedureStep));

            var response = new DicomNSetResponse(
                request,
                result.Success ? DicomStatus.Success : DicomStatus.NoSuchObjectInstance)
            {
                Dataset = BuildMppsEchoDataset(result.Record, dataset),
            };

            ReportAudit(
                state,
                "MppsNSet",
                result.Success ? "Success" : "Failed",
                $"SopInstanceUid={request.SOPInstanceUID?.UID};Status={GetString(dataset, DicomTag.PerformedProcedureStepStatus)}");

            return Task.FromResult(response);
        }

        public Task<DicomNActionResponse> OnNActionRequestAsync(DicomNActionRequest request)
        {
            return Task.FromResult(new DicomNActionResponse(request, DicomStatus.NoSuchActionType));
        }

        public Task<DicomNDeleteResponse> OnNDeleteRequestAsync(DicomNDeleteRequest request)
        {
            return Task.FromResult(new DicomNDeleteResponse(request, DicomStatus.NoSuchObjectInstance));
        }

        public Task<DicomNEventReportResponse> OnNEventReportRequestAsync(DicomNEventReportRequest request)
        {
            return Task.FromResult(new DicomNEventReportResponse(request, DicomStatus.NoSuchEventType));
        }

        public Task<DicomNGetResponse> OnNGetRequestAsync(DicomNGetRequest request)
        {
            return Task.FromResult(new DicomNGetResponse(request, DicomStatus.NoSuchObjectInstance));
        }

        private void ReportAudit(WorkflowScpState state, string eventType, string status, string? detail)
        {
            var timestamp = DateTime.UtcNow;
            state.AuditSink.RecordEvent(new DicomProtocolAuditEvent(
                eventType,
                timestamp,
                DeviceDiscoveryAuditPipeline.BuildDeviceKey(_remoteIp, _callingAeTitle, _calledAeTitle),
                _connectionId,
                _remoteIp,
                _remotePort,
                _callingAeTitle,
                _calledAeTitle,
                status,
                detail));
        }

        private static WorklistQueryCriteria ReadQueryCriteria(DicomDataset dataset)
        {
            var scheduledItem = TryGetFirstSequenceItem(dataset, DicomTag.ScheduledProcedureStepSequence);
            return new WorklistQueryCriteria
            {
                PatientId = GetString(dataset, DicomTag.PatientID),
                PatientName = GetString(dataset, DicomTag.PatientName),
                AccessionNumber = GetString(dataset, DicomTag.AccessionNumber),
                Modality = GetString(scheduledItem, DicomTag.Modality),
                ScheduledStationAeTitle = GetString(scheduledItem, DicomTag.ScheduledStationAETitle),
                ScheduledDateRange = GetString(scheduledItem, DicomTag.ScheduledProcedureStepStartDate),
            };
        }

        private static DicomDataset BuildWorklistDataset(WorklistItemRecord item)
        {
            var scheduledStepItem = new DicomDataset
            {
                { DicomTag.ScheduledStationAETitle, item.ScheduledStationAeTitle },
                { DicomTag.Modality, item.Modality },
                { DicomTag.ScheduledProcedureStepStartDate, item.ScheduledStart.ToString("yyyyMMdd") },
                { DicomTag.ScheduledProcedureStepStartTime, item.ScheduledStart.ToString("HHmmss") },
                { DicomTag.ScheduledProcedureStepDescription, item.ScheduledProcedureStepDescription },
                { DicomTag.ScheduledProcedureStepID, item.ScheduledProcedureStepId },
            };

            return new DicomDataset
            {
                { DicomTag.PatientID, item.PatientId },
                { DicomTag.PatientName, item.PatientName },
                { DicomTag.AccessionNumber, item.AccessionNumber },
                { DicomTag.RequestedProcedureID, item.RequestedProcedureId },
                { DicomTag.StudyInstanceUID, item.StudyInstanceUid },
                { DicomTag.RequestedProcedureDescription, item.RequestedProcedureDescription },
                { DicomTag.ReferringPhysicianName, item.ReferringPhysicianName },
                { DicomTag.PatientSex, item.PatientSex },
                { DicomTag.PatientBirthDate, item.PatientBirthDate },
                new DicomSequence(DicomTag.ScheduledProcedureStepSequence, scheduledStepItem),
            };
        }

        private static DicomDataset BuildMppsEchoDataset(MppsRecord? record, DicomDataset original)
        {
            if (record is null)
            {
                return original;
            }

            return new DicomDataset
            {
                { DicomTag.PatientID, record.PatientId },
                { DicomTag.PatientName, record.PatientName },
                { DicomTag.AccessionNumber, record.AccessionNumber },
                { DicomTag.StudyInstanceUID, record.StudyInstanceUid },
                { DicomTag.PerformedProcedureStepID, record.PerformedProcedureStepId },
                { DicomTag.PerformedProcedureStepStatus, record.Status },
            };
        }

        private static DicomDataset TryGetFirstSequenceItem(DicomDataset dataset, DicomTag tag)
        {
            return dataset.TryGetSequence(tag, out var sequence) && sequence.Items.Count > 0
                ? sequence.Items[0]
                : new DicomDataset();
        }

        private static string GetString(DicomDataset dataset, DicomTag tag)
        {
            return dataset.TryGetSingleValue(tag, out string? value) ? value?.Trim() ?? string.Empty : string.Empty;
        }

        private static string NormalizeAe(string? ae)
        {
            return string.IsNullOrWhiteSpace(ae) ? "UNKNOWN" : ae.Trim();
        }

        private static (string ip, int? port) TryGetRemoteEndpoint(INetworkStream stream, ILogger logger)
        {
            try
            {
                if (stream is NetworkStream networkStream && networkStream.Socket?.RemoteEndPoint is IPEndPoint socketEndPoint)
                {
                    return (socketEndPoint.Address.ToString(), socketEndPoint.Port);
                }

                if (TryExtractEndpoint(stream, logger, out var endpoint))
                {
                    return endpoint;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve remote endpoint for DICOM connection.");
            }

            return ("unknown", null);
        }

        private static bool TryExtractEndpoint(object source, ILogger logger, out (string ip, int? port) endpoint)
        {
            endpoint = ("unknown", null);

            if (TryGetIpEndpoint(source, out var direct))
            {
                endpoint = direct;
                return true;
            }

            if (TryGetProperty(source, "Socket", out var socketObject) && socketObject is Socket socket && socket.RemoteEndPoint is IPEndPoint socketRemote)
            {
                endpoint = (socketRemote.Address.ToString(), socketRemote.Port);
                return true;
            }

            if (TryGetProperty(source, "TcpClient", out var tcpClientObject)
                && tcpClientObject is not null
                && TryGetProperty(tcpClientObject, "Client", out var clientSocketObject)
                && clientSocketObject is Socket clientSocket
                && clientSocket.RemoteEndPoint is IPEndPoint clientRemote)
            {
                endpoint = (clientRemote.Address.ToString(), clientRemote.Port);
                return true;
            }

            if (TryGetProperty(source, "InnerStream", out var innerStreamObject) && innerStreamObject is not null)
            {
                return TryExtractEndpoint(innerStreamObject, logger, out endpoint);
            }

            return false;
        }

        private static bool TryGetIpEndpoint(object source, out (string ip, int? port) endpoint)
        {
            endpoint = ("unknown", null);

            if (source is EndPoint endPoint && endPoint is IPEndPoint ipEndPoint)
            {
                endpoint = (ipEndPoint.Address.ToString(), ipEndPoint.Port);
                return true;
            }

            if (TryGetProperty(source, "RemoteEndPoint", out var remoteEndPointObject)
                && remoteEndPointObject is IPEndPoint remoteEndPoint)
            {
                endpoint = (remoteEndPoint.Address.ToString(), remoteEndPoint.Port);
                return true;
            }

            return false;
        }

        private static bool TryGetProperty(object source, string propertyName, out object? value)
        {
            value = null;
            var property = source.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is null || property.GetIndexParameters().Length > 0)
            {
                return false;
            }

            try
            {
                value = property.GetValue(source);
                return value is not null;
            }
            catch
            {
                return false;
            }
        }
    }
}
