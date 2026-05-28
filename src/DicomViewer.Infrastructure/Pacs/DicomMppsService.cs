using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// 使用标准 DICOM N-CREATE/N-SET 上报 MPPS。
/// 当前先实现 Started（IN PROGRESS）的最小闭环，用于在曝光前建立标准执行态。
/// </summary>
public sealed class DicomMppsService : IMppsService
{
    // 1.2.840.10008.3.1.2.3.3 = Modality Performed Procedure Step SOP Class
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
        if (string.IsNullOrWhiteSpace(configuration.MppsHost) || configuration.MppsPort <= 0)
        {
            return new MppsSubmitResult(
                false,
                "MPPS Started 失败",
                "未配置 MPPS 目标主机或端口。",
                session.MppsInstanceUid ?? string.Empty,
                DateTime.UtcNow);
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
                // 当前版本只构建 Started 所需的最小数据集，后续再补完整字段。
                Dataset = BuildStartedDataset(session, configuration),
            };

            MppsSubmitResult? result = null;
            request.OnResponseReceived += (_, response) =>
            {
                var state = response.Status?.State;
                var isSuccess = state is DicomState.Success or DicomState.Warning;
                result = new MppsSubmitResult(
                    isSuccess,
                    isSuccess ? "MPPS Started 成功" : "MPPS Started 失败",
                    response.Status?.Description ?? response.Status?.ToString() ?? "未收到状态描述。",
                    sopInstanceUid,
                    DateTime.UtcNow);
            };

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

            return result ?? new MppsSubmitResult(
                false,
                "MPPS Started 失败",
                "未收到 N-CREATE 终态响应。",
                sopInstanceUid,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new MppsSubmitResult(
                false,
                "MPPS Started 失败",
                ex.Message,
                sopInstanceUid,
                DateTime.UtcNow);
        }
    }

    public Task<MppsSubmitResult> CompleteAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MppsSubmitResult(
            false,
            "MPPS Completed 暂未实现",
            "当前阶段只实现 Started（N-CREATE），Completed 将在下一阶段接入。",
            session.MppsInstanceUid ?? string.Empty,
            DateTime.UtcNow));
    }

    public Task<MppsSubmitResult> DiscontinueAsync(ExamSession session, string reason, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MppsSubmitResult(
            false,
            "MPPS Discontinued 暂未实现",
            $"当前阶段只实现 Started（N-CREATE），Discontinued 将在下一阶段接入。原因: {reason}",
            session.MppsInstanceUid ?? string.Empty,
            DateTime.UtcNow));
    }

    private static DicomDataset BuildStartedDataset(ExamSession session, PacsConfiguration configuration)
    {
        var now = DateTime.Now;
        var startedDate = now.ToString("yyyyMMdd");
        var startedTime = now.ToString("HHmmss");

        // ScheduledStepAttributesSequence 是 MPPS 里最常用的关联锚点。
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
}