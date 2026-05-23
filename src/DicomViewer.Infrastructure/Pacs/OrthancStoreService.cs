using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;

namespace DicomViewer.Infrastructure.Pacs;

public sealed class OrthancStoreService : IPacsStoreService
{
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
}