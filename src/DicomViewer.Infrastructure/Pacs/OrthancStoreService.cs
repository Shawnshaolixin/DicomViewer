using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DicomViewer.Infrastructure.Pacs;

/// <summary>
/// 基于 fo-dicom 网络客户端实现 PACS 验证与 C-STORE 发送。
/// 默认目标场景是兼容 Orthanc 的学习环境。
/// </summary>
public sealed class OrthancStoreService : IPacsStoreService
{
    private const int QueryLimit = 20;
    private readonly ILocalDicomStoreScpService _localDicomStoreScpService;

    public OrthancStoreService(ILocalDicomStoreScpService localDicomStoreScpService)
    {
        _localDicomStoreScpService = localDicomStoreScpService;
    }

    /// <summary>
    /// 发送 C-ECHO 请求，验证远端 PACS AE 是否可达。
    /// </summary>
    public async Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = DicomClientFactory.Create(
                configuration.Host,
                configuration.Port,
                false,
                configuration.CallingAeTitle,
                configuration.CalledAeTitle);

            PacsStoreResult? responseResult = null;
            var request = new DicomCEchoRequest();
            request.OnResponseReceived += (_, response) =>
            {
                var isSuccess = response.Status is not null && response.Status.State == DicomState.Success;
                responseResult = new PacsStoreResult(
                    isSuccess,
                    isSuccess ? "PACS 连通性验证成功" : "PACS 连通性验证失败",
                    response.Status?.Description ?? response.Status?.ToString() ?? "未收到 C-ECHO 响应。",
                    configuration.CalledAeTitle,
                    configuration.Host,
                    configuration.Port,
                    string.Empty,
                    DateTime.UtcNow);
            };

            await client.AddRequestAsync(request);
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

            return responseResult ?? new PacsStoreResult(
                false,
                "PACS 连通性验证失败",
                "未收到 C-ECHO 响应。",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                string.Empty,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsStoreResult(
                false,
                "PACS 连通性验证失败",
                ex.Message,
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                string.Empty,
                DateTime.UtcNow);
        }
    }

    /// <summary>
    /// 使用 C-STORE 将本地 DICOM 文件发送到远端 PACS。
    /// </summary>
    public async Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dicomFilePath) || !File.Exists(dicomFilePath))
        {
            return new PacsStoreResult(
                false,
                "PACS 发送失败",
                $"DICOM 文件不存在: {dicomFilePath}",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                dicomFilePath,
                DateTime.UtcNow);
        }

        try
        {
            var client = DicomClientFactory.Create(
                configuration.Host,
                configuration.Port,
                false,
                configuration.CallingAeTitle,
                configuration.CalledAeTitle);

            PacsStoreResult? responseResult = null;
            var request = new DicomCStoreRequest(dicomFilePath, DicomPriority.Medium);
            request.OnResponseReceived += (_, response) =>
            {
                var isSuccess = response.Status is not null && response.Status.State == DicomState.Success;
                responseResult = new PacsStoreResult(
                    isSuccess,
                    isSuccess ? "PACS 发送成功" : "PACS 发送失败",
                    response.Status?.Description ?? response.Status?.ToString() ?? "未收到状态描述。",
                    configuration.CalledAeTitle,
                    configuration.Host,
                    configuration.Port,
                    dicomFilePath,
                    DateTime.UtcNow);
            };

            await client.AddRequestAsync(request);
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

            return responseResult ?? new PacsStoreResult(
                false,
                "PACS 发送失败",
                "未收到 C-STORE 响应。",
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                dicomFilePath,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsStoreResult(
                false,
                "PACS 发送失败",
                ex.Message,
                configuration.CalledAeTitle,
                configuration.Host,
                configuration.Port,
                dicomFilePath,
                DateTime.UtcNow);
        }
    }

    public async Task<PacsStudyQueryResult> QueryStudiesAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = CreateOrthancHttpClient(configuration);
            using var response = await httpClient.PostAsJsonAsync(
                "tools/find",
                new OrthancFindRequest(
                    "Study",
                    QueryLimit,
                    true,
                    BuildOrthancQuery(criteria),
                    [
                        "StudyDate",
                        "StudyInstanceUID",
                        "StudyDescription",
                        "ModalitiesInStudy",
                        "PatientID",
                        "PatientName",
                        "NumberOfStudyRelatedInstances",
                    ]),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = string.IsNullOrWhiteSpace(errorContent)
                    ? $"Orthanc 查询失败，HTTP {(int)response.StatusCode} ({response.ReasonPhrase})。"
                    : $"Orthanc 查询失败，HTTP {(int)response.StatusCode} ({response.ReasonPhrase})：{errorContent}";

                throw new HttpRequestException(errorMessage, null, response.StatusCode);
            }

            var studies = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrthancStudySearchResponse>>(cancellationToken: cancellationToken)
                ?? Array.Empty<OrthancStudySearchResponse>();

            var result = studies
                .Select(MapStudy)
                .OrderByDescending(study => study.StudyDateUtc)
                .ToArray();

            return new PacsStudyQueryResult(
                true,
                "PACS 查询成功",
                result.Length == 0 ? "Orthanc 中暂无可回取检查。" : $"共查询到 {result.Length} 条远端检查。",
                result,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsStudyQueryResult(
                false,
                "PACS 查询失败",
                ex.Message,
                Array.Empty<PacsRemoteStudy>(),
                DateTime.UtcNow);
        }
    }

    public async Task<PacsRetrieveResult> RetrieveStudyAsync(string remoteStudyId, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remoteStudyId))
        {
            return new PacsRetrieveResult(false, "PACS 回取失败", "远端检查标识为空。", string.Empty, DateTime.UtcNow);
        }

        try
        {
            using var httpClient = CreateOrthancHttpClient(configuration);
            using var studyResponse = await httpClient.GetAsync($"studies/{Uri.EscapeDataString(remoteStudyId)}", cancellationToken);
            studyResponse.EnsureSuccessStatusCode();

            var study = await studyResponse.Content.ReadFromJsonAsync<OrthancStudyDetailResponse>(cancellationToken: cancellationToken);
            var instances = study?.Instances ?? [];

            if (instances.Count == 0)
            {
                return new PacsRetrieveResult(false, "PACS 回取失败", "远端检查中未找到实例。", string.Empty, DateTime.UtcNow);
            }

            Directory.CreateDirectory(targetDirectory);
            foreach (var instanceId in instances)
            {
                using var instanceResponse = await httpClient.GetAsync($"instances/{Uri.EscapeDataString(instanceId)}/file", cancellationToken);
                instanceResponse.EnsureSuccessStatusCode();

                var filePath = Path.Combine(targetDirectory, $"{instanceId}.dcm");
                await using var fileStream = File.Create(filePath);
                await instanceResponse.Content.CopyToAsync(fileStream, cancellationToken);
            }

            return new PacsRetrieveResult(
                true,
                "PACS 回取成功",
                $"已下载 {instances.Count} 个实例到 {targetDirectory}",
                targetDirectory,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsRetrieveResult(false, "PACS 回取失败", ex.Message, string.Empty, DateTime.UtcNow);
        }
    }

    public async Task<PacsStudyQueryResult> QueryStudiesViaDicomAsync(PacsConfiguration configuration, PacsStudyQueryCriteria criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = DicomClientFactory.Create(
                configuration.Host,
                configuration.Port,
                false,
                configuration.CallingAeTitle,
                configuration.CalledAeTitle);

            var studies = new List<PacsRemoteStudy>();
            var request = BuildDicomFindRequest(criteria);
            request.OnResponseReceived += (_, response) =>
            {
                if (response.Status is null || response.Status.State != DicomState.Pending || response.Dataset is null)
                {
                    return;
                }

                studies.Add(MapDicomFindStudy(response.Dataset));
            };

            await client.AddRequestAsync(request);
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

            var result = studies
                .OrderByDescending(study => study.StudyDateUtc)
                .ToArray();

            return new PacsStudyQueryResult(
                true,
                "C-FIND 查询成功",
                result.Length == 0 ? "未查询到符合条件的远端检查。" : $"共查询到 {result.Length} 条远端检查。",
                result,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsStudyQueryResult(false, "C-FIND 查询失败", ex.Message, Array.Empty<PacsRemoteStudy>(), DateTime.UtcNow);
        }
    }

    public async Task<PacsRetrieveResult> RetrieveStudyViaDicomAsync(string studyInstanceUid, string targetDirectory, PacsConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUid))
        {
            return new PacsRetrieveResult(false, "C-MOVE 回取失败", "StudyInstanceUID 为空。", string.Empty, DateTime.UtcNow);
        }

        try
        {
            var receiveSession = _localDicomStoreScpService.PrepareReceive(configuration, targetDirectory);
            await RegisterMoveDestinationAsync(configuration, cancellationToken);

            var client = DicomClientFactory.Create(
                configuration.Host,
                configuration.Port,
                false,
                configuration.CallingAeTitle,
                configuration.CalledAeTitle);

            PacsRetrieveResult? retrieveResult = null;
            var request = new DicomCMoveRequest(configuration.CallingAeTitle, studyInstanceUid, DicomPriority.Medium);
            request.OnResponseReceived += (_, response) =>
            {
                if (response.Status is null || response.Status.State == DicomState.Pending)
                {
                    return;
                }

                var isSuccess = response.Status.State == DicomState.Success && receiveSession.ReceivedFiles.Count > 0;
                retrieveResult = new PacsRetrieveResult(
                    isSuccess,
                    isSuccess ? "C-MOVE 回取成功" : "C-MOVE 回取失败",
                    isSuccess
                        ? $"已接收 {receiveSession.ReceivedFiles.Count} 个实例到 {targetDirectory}"
                        : response.Status.Description ?? response.Status.ToString(),
                    isSuccess ? targetDirectory : string.Empty,
                    DateTime.UtcNow);
            };

            await client.AddRequestAsync(request);
            await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

            return retrieveResult ?? new PacsRetrieveResult(
                receiveSession.ReceivedFiles.Count > 0,
                receiveSession.ReceivedFiles.Count > 0 ? "C-MOVE 回取成功" : "C-MOVE 回取失败",
                receiveSession.ReceivedFiles.Count > 0
                    ? $"已接收 {receiveSession.ReceivedFiles.Count} 个实例到 {targetDirectory}"
                    : "未收到 C-MOVE 终态响应。",
                receiveSession.ReceivedFiles.Count > 0 ? targetDirectory : string.Empty,
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            return new PacsRetrieveResult(false, "C-MOVE 回取失败", ex.Message, string.Empty, DateTime.UtcNow);
        }
    }

    private static HttpClient CreateOrthancHttpClient(PacsConfiguration configuration)
    {
        return new HttpClient
        {
            BaseAddress = BuildOrthancBaseUri(configuration),
        };
    }

    private static Uri BuildOrthancBaseUri(PacsConfiguration configuration)
    {
        if (Uri.TryCreate(configuration.Host, UriKind.Absolute, out var absoluteUri))
        {
            var builder = new UriBuilder(absoluteUri)
            {
                Port = configuration.RestApiPort,
            };

            return builder.Uri;
        }

        return new UriBuilder(Uri.UriSchemeHttp, configuration.Host, configuration.RestApiPort).Uri;
    }

    private static PacsRemoteStudy MapStudy(OrthancStudySearchResponse response)
    {
        var requestedTags = response.RequestedTags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        requestedTags.TryGetValue("StudyInstanceUID", out var studyInstanceUid);
        requestedTags.TryGetValue("PatientID", out var patientId);
        requestedTags.TryGetValue("PatientName", out var patientName);
        requestedTags.TryGetValue("StudyDescription", out var studyDescription);
        requestedTags.TryGetValue("ModalitiesInStudy", out var modalitiesInStudy);
        requestedTags.TryGetValue("NumberOfStudyRelatedInstances", out var relatedInstances);
        requestedTags.TryGetValue("StudyDate", out var studyDateText);

        return new PacsRemoteStudy(
            response.Id,
            studyInstanceUid ?? string.Empty,
            patientId ?? string.Empty,
            patientName ?? string.Empty,
            string.IsNullOrWhiteSpace(studyDescription) ? "未命名检查" : studyDescription,
            string.IsNullOrWhiteSpace(modalitiesInStudy) ? "OT" : modalitiesInStudy,
            int.TryParse(relatedInstances, out var instanceCount) ? instanceCount : 0,
            ParseStudyDate(studyDateText));
    }

    private static Dictionary<string, string> BuildOrthancQuery(PacsStudyQueryCriteria criteria)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddQueryValue(query, "PatientName", criteria.PatientName);
        AddQueryValue(query, "PatientID", criteria.PatientId);
        AddQueryValue(query, "StudyDescription", criteria.StudyDescription);
        AddQueryValue(query, "ModalitiesInStudy", criteria.Modality);

        var studyDateRange = BuildStudyDateRange(criteria.StudyDateFromUtc, criteria.StudyDateToUtc);
        if (!string.IsNullOrWhiteSpace(studyDateRange))
        {
            query["StudyDate"] = studyDateRange;
        }

        return query;
    }

    private static DicomCFindRequest BuildDicomFindRequest(PacsStudyQueryCriteria criteria)
    {
        var request = DicomCFindRequest.CreateStudyQuery(
            string.IsNullOrWhiteSpace(criteria.PatientId) ? string.Empty : criteria.PatientId,
            string.IsNullOrWhiteSpace(criteria.PatientName) ? string.Empty : criteria.PatientName,
            BuildDicomDateRange(criteria),
            string.Empty,
            string.IsNullOrWhiteSpace(criteria.StudyDescription) ? string.Empty : criteria.StudyDescription,
            string.Empty,
            string.Empty);

        request.Dataset.AddOrUpdate(DicomTag.ModalitiesInStudy, string.IsNullOrWhiteSpace(criteria.Modality) ? string.Empty : criteria.Modality);
        return request;
    }

    private static DicomDateRange? BuildDicomDateRange(PacsStudyQueryCriteria criteria)
    {
        if (criteria.StudyDateFromUtc is null && criteria.StudyDateToUtc is null)
        {
            return null;
        }

        var from = criteria.StudyDateFromUtc ?? DateTime.MinValue;
        var to = criteria.StudyDateToUtc ?? DateTime.MaxValue;
        return new DicomDateRange(from, to);
    }

    private static PacsRemoteStudy MapDicomFindStudy(DicomDataset dataset)
    {
        return new PacsRemoteStudy(
            string.Empty,
            GetString(dataset, DicomTag.StudyInstanceUID),
            GetString(dataset, DicomTag.PatientID),
            GetString(dataset, DicomTag.PatientName),
            string.IsNullOrWhiteSpace(GetString(dataset, DicomTag.StudyDescription)) ? "未命名检查" : GetString(dataset, DicomTag.StudyDescription),
            string.IsNullOrWhiteSpace(GetString(dataset, DicomTag.ModalitiesInStudy)) ? "OT" : GetString(dataset, DicomTag.ModalitiesInStudy),
            GetInt(dataset, DicomTag.NumberOfStudyRelatedInstances),
            ParseStudyDate(GetString(dataset, DicomTag.StudyDate)));
    }

    private static string GetString(DicomDataset dataset, DicomTag tag)
    {
        return dataset.TryGetSingleValue(tag, out string? value) ? value ?? string.Empty : string.Empty;
    }

    private static int GetInt(DicomDataset dataset, DicomTag tag)
    {
        return dataset.TryGetSingleValue(tag, out int value) ? value : 0;
    }

    private static async Task RegisterMoveDestinationAsync(PacsConfiguration configuration, CancellationToken cancellationToken)
    {
        using var httpClient = CreateOrthancHttpClient(configuration);
        using var response = await httpClient.PutAsJsonAsync(
            $"modalities/{Uri.EscapeDataString(configuration.CallingAeTitle)}",
            new object[] { configuration.CallingAeTitle, configuration.LocalStoreHost, configuration.LocalStorePort },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static void AddQueryValue(IDictionary<string, string> query, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query[key] = value.Trim();
        }
    }

    private static string BuildStudyDateRange(DateTime? studyDateFromUtc, DateTime? studyDateToUtc)
    {
        if (studyDateFromUtc is null && studyDateToUtc is null)
        {
            return string.Empty;
        }

        var from = studyDateFromUtc?.ToString("yyyyMMdd") ?? string.Empty;
        var to = studyDateToUtc?.ToString("yyyyMMdd") ?? string.Empty;
        return $"{from}-{to}";
    }

    private static DateTime? ParseStudyDate(string? studyDateText)
    {
        if (string.IsNullOrWhiteSpace(studyDateText) || studyDateText.Length != 8)
        {
            return null;
        }

        return DateTime.TryParseExact(
            studyDateText,
            "yyyyMMdd",
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private sealed record OrthancFindRequest(
        [property: JsonPropertyName("Level")] string Level,
        [property: JsonPropertyName("Limit")] int Limit,
        [property: JsonPropertyName("Expand")] bool Expand,
        [property: JsonPropertyName("Query")] Dictionary<string, string> Query,
        [property: JsonPropertyName("RequestedTags")] string[] RequestedTags);

    private sealed class OrthancStudySearchResponse
    {
        [JsonPropertyName("ID")]
        public string Id { get; init; } = string.Empty;

        public Dictionary<string, string>? RequestedTags { get; init; }
    }

    private sealed class OrthancStudyDetailResponse
    {
        public List<string> Instances { get; init; } = [];
    }
}