using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 根据检查会话生成模拟曝光结果。
/// </summary>
public interface IExposureSimulationService
{
    /// <summary>
    /// 执行模拟曝光并输出结果文件。
    /// </summary>
    Task<ExposureResult> RunAsync(ExamSession session, string outputDirectory, CancellationToken cancellationToken = default);
}