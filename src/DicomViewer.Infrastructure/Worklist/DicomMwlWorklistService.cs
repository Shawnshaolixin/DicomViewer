using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace DicomViewer.Infrastructure.Worklist;

/// <summary>
/// 通过标准 DICOM MWL C-FIND 查询工作列表。
/// 该实现按当前控制台配置发起查询，并在失败时回退到模拟工作列表，确保学习链路可用。
/// </summary>
public sealed class DicomMwlWorklistService : IWorklistService
{
    private readonly IConsoleConfigurationStore _consoleConfigurationStore;
    private readonly MockWorklistService _mockWorklistService;

    public DicomMwlWorklistService(IConsoleConfigurationStore consoleConfigurationStore, MockWorklistService mockWorklistService)
    {
        _consoleConfigurationStore = consoleConfigurationStore;
        _mockWorklistService = mockWorklistService;
    }

    public async Task<IReadOnlyList<ImagingOrder>> QueryAsync(MwlQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        // 每次查询前都读取最新配置，便于在 UI 修改配置后立即生效。
        var configuration = _consoleConfigurationStore.Load().PacsConfiguration;

        // 初学阶段优先保证流程可运行：若 MWL 目标未配置，直接回退到 Mock 数据。
        if (string.IsNullOrWhiteSpace(configuration.MwlHost) || configuration.MwlPort <= 0)
        {
            return await _mockWorklistService.QueryAsync(criteria, cancellationToken);
        }

        try
        {
            var orders = new List<ImagingOrder>();
            var request = BuildWorklistQuery(criteria, configuration);
            request.OnResponseReceived += (_, response) =>
            {
                // MWL C-FIND 的有效结果在 Pending 响应里；终态响应不携带业务数据。
                if (response.Status is null || response.Status.State != DicomState.Pending || response.Dataset is null)
                {
                    return;
                }

                orders.Add(MapMwlDataset(response.Dataset));
            };

            var client = DicomClientFactory.Create(
                configuration.MwlHost,
                configuration.MwlPort,
                false,
                configuration.CallingAeTitle,
                configuration.MwlCalledAeTitle);

            await client.AddRequestAsync(request);
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

            // 查询成功但无结果时也回退到 Mock，避免初学者在空环境里卡住。
            if (orders.Count == 0)
            {
                return await _mockWorklistService.QueryAsync(criteria, cancellationToken);
            }

            return orders
                .OrderBy(order => order.ScheduledStartDateTime ?? order.ScheduledTime)
                .ToArray();
        }
        catch
        {
            // 网络或协议异常统一降级到 Mock，保证主流程（选单-曝光-回看）不断。
            return await _mockWorklistService.QueryAsync(criteria, cancellationToken);
        }
    }

    private static DicomCFindRequest BuildWorklistQuery(MwlQueryCriteria criteria, PacsConfiguration configuration)
    {
        // 使用 fo-dicom 的 CreateWorklistQuery，避免手写基础查询标签。
        var request = DicomCFindRequest.CreateWorklistQuery(
            string.IsNullOrWhiteSpace(criteria.PatientId) ? null : criteria.PatientId,
            string.IsNullOrWhiteSpace(criteria.PatientName) ? null : criteria.PatientName,
            string.IsNullOrWhiteSpace(criteria.ScheduledStationAeTitle) ? configuration.StationAeTitle : criteria.ScheduledStationAeTitle,
            string.IsNullOrWhiteSpace(configuration.StationName) ? null : configuration.StationName,
            string.IsNullOrWhiteSpace(criteria.Modality) ? null : criteria.Modality,
            BuildScheduledDateRange(criteria));

        // Accession Number 作为可选过滤键，单独补充到查询 Dataset。
        if (!string.IsNullOrWhiteSpace(criteria.AccessionNumber))
        {
            request.Dataset.AddOrUpdate(DicomTag.AccessionNumber, criteria.AccessionNumber.Trim());
        }

        return request;
    }

    private static DicomDateRange? BuildScheduledDateRange(MwlQueryCriteria criteria)
    {
        if (criteria.ScheduledDateFrom is null && criteria.ScheduledDateTo is null)
        {
            return null;
        }

        var from = criteria.ScheduledDateFrom ?? DateTime.MinValue;
        var to = criteria.ScheduledDateTo ?? DateTime.MaxValue;
        return new DicomDateRange(from, to);
    }

    private static ImagingOrder MapMwlDataset(DicomDataset dataset)
    {
        // 关键执行信息主要位于 SPS 序列中，先取第一条作为最小实现。
        var spsItem = TryGetSpsItem(dataset);
        var scheduledDateText = GetString(spsItem, DicomTag.ScheduledProcedureStepStartDate);
        var scheduledTimeText = GetString(spsItem, DicomTag.ScheduledProcedureStepStartTime);
        var scheduledStart = ParseDicomDateTime(scheduledDateText, scheduledTimeText);

        var bodyPart = GetString(spsItem, DicomTag.BodyPartExamined);
        var procedureDescription = GetString(spsItem, DicomTag.ScheduledProcedureStepDescription);
        if (string.IsNullOrWhiteSpace(procedureDescription))
        {
            procedureDescription = GetString(dataset, DicomTag.RequestedProcedureDescription);
        }

        var modality = GetString(spsItem, DicomTag.Modality);
        var stationAeTitle = GetString(spsItem, DicomTag.ScheduledStationAETitle);
        var orderId = GetString(dataset, DicomTag.AccessionNumber);
        if (string.IsNullOrWhiteSpace(orderId))
        {
            orderId = GetString(spsItem, DicomTag.ScheduledProcedureStepID);
        }
        if (string.IsNullOrWhiteSpace(orderId))
        {
            // 最后兜底：即使上游缺字段，也给本地会话一个可追踪主键。
            orderId = Guid.NewGuid().ToString("N");
        }

        var bodyPartText = string.IsNullOrWhiteSpace(bodyPart) ? "UNKNOWN" : bodyPart;
        return new ImagingOrder
        {
            OrderId = orderId,
            PatientId = GetString(dataset, DicomTag.PatientID),
            PatientName = GetString(dataset, DicomTag.PatientName),
            AccessionNumber = GetString(dataset, DicomTag.AccessionNumber),
            RequestedProcedureId = GetString(dataset, DicomTag.RequestedProcedureID),
            ScheduledProcedureStepId = GetString(spsItem, DicomTag.ScheduledProcedureStepID),
            StudyInstanceUid = GetString(dataset, DicomTag.StudyInstanceUID),
            Modality = string.IsNullOrWhiteSpace(modality) ? "OT" : modality,
            ScheduledStationAeTitle = stationAeTitle,
            ScheduledStartDateTime = scheduledStart,
            ReferringPhysicianName = GetString(dataset, DicomTag.ReferringPhysicianName),
            PatientSex = GetString(dataset, DicomTag.PatientSex),
            PatientBirthDate = ParseDicomDate(GetString(dataset, DicomTag.PatientBirthDate)),
            RequestedProcedureDescription = GetString(dataset, DicomTag.RequestedProcedureDescription),
            SourceType = "MWL",
            ProcedureDescription = string.IsNullOrWhiteSpace(procedureDescription) ? "未命名检查" : procedureDescription,
            BodyPart = bodyPartText,
            Projection = GetProjectionText(procedureDescription),
            ScheduledTime = scheduledStart ?? DateTime.Now,
            Status = "Scheduled",
        };
    }

    private static DicomDataset TryGetSpsItem(DicomDataset dataset)
    {
        if (!dataset.TryGetSequence(DicomTag.ScheduledProcedureStepSequence, out var sequence) || sequence.Items.Count == 0)
        {
            // 返回空 Dataset，后续读取统一得到空字符串，简化空值分支。
            return new DicomDataset();
        }

        return sequence.Items[0];
    }

    private static string GetProjectionText(string procedureDescription)
    {
        if (string.IsNullOrWhiteSpace(procedureDescription))
        {
            return "UNKNOWN";
        }

        var parts = procedureDescription.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "UNKNOWN" : parts[^1].ToUpperInvariant();
    }

    private static string GetString(DicomDataset dataset, DicomTag tag)
    {
        return dataset.TryGetSingleValue(tag, out string? value) ? value?.Trim() ?? string.Empty : string.Empty;
    }

    private static DateTime? ParseDicomDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? ParseDicomDateTime(string date, string time)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return null;
        }

        // DICOM TM 可能包含小数秒，这里取秒级并补齐到 HHmmss。
        var normalizedTime = string.IsNullOrWhiteSpace(time) ? "000000" : time.Split('.')[0].PadRight(6, '0');
        var combined = $"{date}{normalizedTime}";

        return DateTime.TryParseExact(combined, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }
}