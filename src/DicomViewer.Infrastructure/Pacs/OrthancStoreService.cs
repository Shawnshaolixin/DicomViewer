using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
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

    public async Task<PacsStudyQueryResult> QueryStudiesAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default)
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
                    new Dictionary<string, string>(),
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

            response.EnsureSuccessStatusCode();

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
        string Level,
        int Limit,
        bool Expand,
        Dictionary<string, string> Query,
        string[] RequestedTags);

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