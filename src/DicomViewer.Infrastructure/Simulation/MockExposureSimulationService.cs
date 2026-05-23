using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using DicomViewer.Infrastructure.Dicom;

namespace DicomViewer.Infrastructure.Simulation;

/// <summary>
/// 通过构造模拟像素和 DICOM 文件来完成一次“教学用”曝光。
/// </summary>
public sealed class MockExposureSimulationService : IExposureSimulationService
{
    private readonly SimulatedDicomBuilder _dicomBuilder;

    public MockExposureSimulationService(SimulatedDicomBuilder dicomBuilder)
    {
        _dicomBuilder = dicomBuilder;
    }

    /// <summary>
    /// 生成模拟影像文件，并返回供控制台展示的摘要信息。
    /// </summary>
    public async Task<ExposureResult> RunAsync(ExamSession session, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var acquiredAtUtc = DateTime.UtcNow;
        var imageId = $"SIM-{session.Order.OrderId}-{acquiredAtUtc:yyyyMMddHHmmss}";
        var artifactPath = await _dicomBuilder.BuildAsync(session, imageId, acquiredAtUtc, outputDirectory, cancellationToken);
        var previewText = $"模拟图像已生成并封装为 DICOM: {session.Order.PatientName} / {session.ExposureParameters.BodyPart} / kV {session.ExposureParameters.KilovoltagePeak:0.#} / mAs {session.ExposureParameters.MilliampereSeconds:0.#}";
        return new ExposureResult(imageId, previewText, artifactPath, acquiredAtUtc);
    }
}