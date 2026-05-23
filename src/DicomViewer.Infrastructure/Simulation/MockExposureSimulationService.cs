using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;
using DicomViewer.Infrastructure.Dicom;

namespace DicomViewer.Infrastructure.Simulation;

public sealed class MockExposureSimulationService : IExposureSimulationService
{
    private readonly SimulatedDicomBuilder _dicomBuilder;

    public MockExposureSimulationService(SimulatedDicomBuilder dicomBuilder)
    {
        _dicomBuilder = dicomBuilder;
    }

    public async Task<ExposureResult> RunAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        var acquiredAtUtc = DateTime.UtcNow;
        var imageId = $"SIM-{session.Order.OrderId}-{acquiredAtUtc:yyyyMMddHHmmss}";
        var artifactPath = await _dicomBuilder.BuildAsync(session, imageId, acquiredAtUtc, cancellationToken);
        var previewText = $"模拟图像已生成并封装为 DICOM: {session.Order.PatientName} / {session.ExposureParameters.BodyPart} / kV {session.ExposureParameters.KilovoltagePeak:0.#} / mAs {session.ExposureParameters.MilliampereSeconds:0.#}";
        return new ExposureResult(imageId, previewText, artifactPath, acquiredAtUtc);
    }
}