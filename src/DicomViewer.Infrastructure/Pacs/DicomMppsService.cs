using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// 使用标准 DICOM N-CREATE/N-SET 上报 MPPS。
/// </summary>
public sealed class DicomMppsService : IMppsService
{
    private static readonly DicomUID ModalityPerformedProcedureStepSopClass = new(
        "1.2.840.10008.3.1.2.3.3",
        "Modality Performed Procedure Step SOP Class",
        DicomUidType.SOPClass);

    private readonly IConsoleConfigurationStore _consoleConfigurationStore;

    public DicomMppsService(IConsoleConfigurationStore consoleConfigurationStore)
    {
        _consoleConfigurationStore = consoleConfigurationStore;
    }

    public async Task<MppsSubmitResult> CreateInProgressAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        var configuration = _consoleConfigurationStore.Load().PacsConfiguration;
        var configurationError = ValidateConfiguration(configuration, "MPPS Started 失败", session.MppsInstanceUid ?? string.Empty);
        if (configurationError is not null)
        {
            return configurationError;
        }

        var sopInstanceUid = string.IsNullOrWhiteSpace(session.MppsInstanceUid)
            ? DicomUIDGenerator.GenerateDerivedFromUUID().UID
            : session.MppsInstanceUid;

        try
        {
            var request = new DicomNCreateRequest(
                ModalityPerformedProcedureStepSopClass,
                new DicomUID(sopInstanceUid, null, DicomUidType.SOPInstance))
            {
                Dataset = BuildStartedDataset(session, configuration),
            };

            return await SubmitCreateAsync(configuration, request, sopInstanceUid, "MPPS Started", cancellationToken);
        }
        catch (Exception ex)
        {
            return new MppsSubmitResult(false, "MPPS Started 失败", ex.Message, sopInstanceUid, DateTime.UtcNow);
        }
    }

    public async Task<MppsSubmitResult> CompleteAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        var configuration = _consoleConfigurationStore.Load().PacsConfiguration;
        var sopInstanceUid = session.MppsInstanceUid ?? string.Empty;
        var configurationError = ValidateConfiguration(configuration, "MPPS Completed 失败", sopInstanceUid);
        if (configurationError is not null)
        {
            return configurationError;
        }

        if (string.IsNullOrWhiteSpace(sopInstanceUid))
        {
            return new MppsSubmitResult(false, "MPPS Completed 失败", "当前会话尚未生成 MPPS SOP Instance UID。", string.Empty, DateTime.UtcNow);
        }

        try
        {
            var request = new DicomNSetRequest(
                ModalityPerformedProcedureStepSopClass,
                new DicomUID(sopInstanceUid, null, DicomUidType.SOPInstance))
            {
                Dataset = BuildFinalDataset(session, "COMPLETED"),
            };

            return await SubmitSetAsync(configuration, request, sopInstanceUid, "MPPS Completed", cancellationToken);
        }
        catch (Exception ex)
        {
            return new MppsSubmitResult(false, "MPPS Completed 失败", ex.Message, sopInstanceUid, DateTime.UtcNow);
        }
    }

    public async Task<MppsSubmitResult> DiscontinueAsync(ExamSession session, string reason, CancellationToken cancellationToken = default)
    {
        var configuration = _consoleConfigurationStore.Load().PacsConfiguration;
        var sopInstanceUid = session.MppsInstanceUid ?? string.Empty;
        var configurationError = ValidateConfiguration(configuration, "MPPS Discontinued 失败", sopInstanceUid);
        if (configurationError is not null)
        {
            return configurationError;
        }

        if (string.IsNullOrWhiteSpace(sopInstanceUid))
        {
            return new MppsSubmitResult(false, "MPPS Discontinued 失败", "当前会话尚未生成 MPPS SOP Instance UID。", string.Empty, DateTime.UtcNow);
        }

        try
        {
            var request = new DicomNSetRequest(
                ModalityPerformedProcedureStepSopClass,
                new DicomUID(sopInstanceUid, null, DicomUidType.SOPInstance))
            {
                Dataset = BuildFinalDataset(session, "DISCONTINUED", reason),
            };

            return await SubmitSetAsync(configuration, request, sopInstanceUid, "MPPS Discontinued", cancellationToken);
        }
        catch (Exception ex)
        {
            return new MppsSubmitResult(false, "MPPS Discontinued 失败", ex.Message, sopInstanceUid, DateTime.UtcNow);
        }
    }

    private static DicomDataset BuildStartedDataset(ExamSession session, PacsConfiguration configuration)
    {
        var now = DateTime.Now;
        var startedDate = now.ToString("yyyyMMdd");
        var startedTime = now.ToString("HHmmss");

        var scheduledStepAttributes = new DicomDataset();
        if (!string.IsNullOrWhiteSpace(session.ScheduledProcedureStepIdSnapshot))
        {
            scheduledStepAttributes.AddOrUpdate(DicomTag.ScheduledProcedureStepID, session.ScheduledProcedureStepIdSnapshot);
        }

        if (!string.IsNullOrWhiteSpace(session.AccessionNumberSnapshot))
        {
            scheduledStepAttributes.AddOrUpdate(DicomTag.AccessionNumber, session.AccessionNumberSnapshot);
        }

        if (!string.IsNullOrWhiteSpace(session.Order.StudyInstanceUid))
        {
            scheduledStepAttributes.AddOrUpdate(DicomTag.StudyInstanceUID, session.Order.StudyInstanceUid);
        }

        if (!string.IsNullOrWhiteSpace(session.Order.RequestedProcedureId))
        {
            scheduledStepAttributes.AddOrUpdate(DicomTag.RequestedProcedureID, session.Order.RequestedProcedureId);
        }

        return new DicomDataset
        {
            { DicomTag.PatientID, session.Order.PatientId },
            { DicomTag.PatientName, session.Order.PatientName },
            { DicomTag.AccessionNumber, session.Order.AccessionNumber },
            { DicomTag.StudyInstanceUID, session.Order.StudyInstanceUid },
            { DicomTag.PerformedProcedureStepID, session.SessionId },
            { DicomTag.PerformedProcedureStepStartDate, startedDate },
            { DicomTag.PerformedProcedureStepStartTime, startedTime },
            { DicomTag.PerformedProcedureStepStatus, "IN PROGRESS" },
            { DicomTag.PerformedStationAETitle, configuration.StationAeTitle },
            { DicomTag.PerformedStationName, configuration.StationName },
            new DicomSequence(DicomTag.ScheduledStepAttributesSequence, scheduledStepAttributes),
        };
    }

    private static DicomDataset BuildFinalDataset(ExamSession session, string status, string? reason = null)
    {
        var now = DateTime.Now;
        var dataset = new DicomDataset
        {
            { DicomTag.PerformedProcedureStepStatus, status },
            { DicomTag.PerformedProcedureStepEndDate, now.ToString("yyyyMMdd") },
            { DicomTag.PerformedProcedureStepEndTime, now.ToString("HHmmss") },
        };

        if (!string.IsNullOrWhiteSpace(session.LastGeneratedArtifact))
        {
            dataset.AddOrUpdate(DicomTag.PerformedProcedureStepDescription, Path.GetFileNameWithoutExtension(session.LastGeneratedArtifact));
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            dataset.AddOrUpdate(DicomTag.CommentsOnThePerformedProcedureStep, reason);
        }

        return dataset;
    }

    private static MppsSubmitResult? ValidateConfiguration(PacsConfiguration configuration, string failureStatusText, string sopInstanceUid)
    {
        if (!string.IsNullOrWhiteSpace(configuration.MppsHost) && configuration.MppsPort > 0)
        {
            return null;
        }

        return new MppsSubmitResult(false, failureStatusText, "未配置 MPPS 目标主机或端口。", sopInstanceUid, DateTime.UtcNow);
    }

    private static async Task<MppsSubmitResult> SubmitCreateAsync(
        PacsConfiguration configuration,
        DicomNCreateRequest request,
        string sopInstanceUid,
        string actionName,
        CancellationToken cancellationToken)
    {
        MppsSubmitResult? result = null;
        request.OnResponseReceived += (_, response) =>
        {
            result = BuildSubmitResult(response.Status, $"{actionName} 成功", $"{actionName} 失败", sopInstanceUid);
        };

        await SendAsync(configuration, request, cancellationToken);
        return result ?? new MppsSubmitResult(false, $"{actionName} 失败", $"未收到 {actionName} 终态响应。", sopInstanceUid, DateTime.UtcNow);
    }

    private static async Task<MppsSubmitResult> SubmitSetAsync(
        PacsConfiguration configuration,
        DicomNSetRequest request,
        string sopInstanceUid,
        string actionName,
        CancellationToken cancellationToken)
    {
        MppsSubmitResult? result = null;
        request.OnResponseReceived += (_, response) =>
        {
            result = BuildSubmitResult(response.Status, $"{actionName} 成功", $"{actionName} 失败", sopInstanceUid);
        };

        await SendAsync(configuration, request, cancellationToken);
        return result ?? new MppsSubmitResult(false, $"{actionName} 失败", $"未收到 {actionName} 终态响应。", sopInstanceUid, DateTime.UtcNow);
    }

    private static MppsSubmitResult BuildSubmitResult(DicomStatus? status, string successText, string failureText, string sopInstanceUid)
    {
        var state = status?.State;
        var isSuccess = state is DicomState.Success or DicomState.Warning;
        return new MppsSubmitResult(
            isSuccess,
            isSuccess ? successText : failureText,
            status?.Description ?? status?.ToString() ?? "未收到状态描述。",
            sopInstanceUid,
            DateTime.UtcNow);
    }

    private static async Task SendAsync(PacsConfiguration configuration, DicomRequest request, CancellationToken cancellationToken)
    {
        var client = DicomClientFactory.Create(
            configuration.MppsHost,
            configuration.MppsPort,
            false,
            configuration.CallingAeTitle,
            string.IsNullOrWhiteSpace(configuration.MppsCalledAeTitle)
                ? configuration.CalledAeTitle
                : configuration.MppsCalledAeTitle);

        await client.AddRequestAsync(request);
        await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);
    }
}
