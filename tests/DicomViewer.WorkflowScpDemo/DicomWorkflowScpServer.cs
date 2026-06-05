using System.Runtime.CompilerServices;
using System.Text;
using FellowOakDicom;
using FellowOakDicom.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace DicomViewer.WorkflowScpDemo;

internal sealed class DicomWorkflowScpServer : IDisposable
{
    private readonly WorkflowScpState _state;
    private IDicomServer? _server;

    public DicomWorkflowScpServer(WorkflowDataStore store, WorkflowServerOptions options)
    {
        _state = new WorkflowScpState(store, options.CalledAeTitle);
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

    private sealed record WorkflowScpState(WorkflowDataStore Store, string CalledAeTitle);

    private sealed class WorkflowScpService : DicomService, IDicomServiceProvider, IDicomCFindProvider, IDicomNServiceProvider
    {
        public WorkflowScpService(INetworkStream stream, Encoding fallbackEncoding, Microsoft.Extensions.Logging.ILogger logger, DicomServiceDependencies dependencies)
            : base(stream, fallbackEncoding, logger, dependencies)
        {
        }

        public async Task OnReceiveAssociationRequestAsync(DicomAssociation association)
        {
            if (UserState is not WorkflowScpState state || !string.Equals(state.CalledAeTitle, association.CalledAE, StringComparison.OrdinalIgnoreCase))
            {
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

        public async IAsyncEnumerable<DicomCFindResponse> OnCFindRequestAsync(DicomCFindRequest request)
        {
            if (UserState is not WorkflowScpState state)
            {
                yield return new DicomCFindResponse(request, DicomStatus.ProcessingFailure);
                yield break;
            }

            var criteria = ReadQueryCriteria(request.Dataset);
            foreach (var item in state.Store.QueryWorklist(criteria))
            {
                yield return new DicomCFindResponse(request, DicomStatus.Pending)
                {
                    Dataset = BuildWorklistDataset(item),
                };
            }

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
                SOPInstanceUID = request.SOPInstanceUID,
                Dataset = BuildMppsEchoDataset(result.Record, dataset),
            };

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
                SOPInstanceUID = request.SOPInstanceUID,
                Dataset = BuildMppsEchoDataset(result.Record, dataset),
            };

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

        [EnumeratorCancellation]
        private static async IAsyncEnumerable<DicomCFindResponse> Empty([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
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
    }
}
