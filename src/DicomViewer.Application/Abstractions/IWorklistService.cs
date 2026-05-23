using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 提供检查工作列表数据源。
/// </summary>
public interface IWorklistService
{
    /// <summary>
    /// 加载可供控制台选择的检查任务列表。
    /// </summary>
    Task<IReadOnlyList<ImagingOrder>> LoadAsync(CancellationToken cancellationToken = default);
}