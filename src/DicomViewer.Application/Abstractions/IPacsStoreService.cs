using DicomViewer.Application.Models;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 定义 PACS 连通性验证与影像发送能力。
/// </summary>
public interface IPacsStoreService
{
    /// <summary>
    /// 使用当前配置执行一次 C-ECHO 连通性验证。
    /// </summary>
    Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将指定 DICOM 文件发送到 PACS。
    /// </summary>
    Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default);
}