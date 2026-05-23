using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 提供影像目录加载能力，并把结果转换为领域层患者/检查结构。
/// </summary>
public interface IStudyCatalogService
{
    /// <summary>
    /// 加载指定目录或默认样例数据。
    /// </summary>
    Task<StudyCatalogLoadResult> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default);
}