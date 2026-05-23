using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Infrastructure.Simulation;

public sealed class MockExposureSimulationService : IExposureSimulationService
{
    public Task<ExposureResult> RunAsync(ExamSession session, CancellationToken cancellationToken = default)
    {
        var imageId = $"SIM-{session.Order.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var previewText = $"模拟图像已生成: {session.Order.PatientName} / {session.ExposureParameters.BodyPart} / kV {session.ExposureParameters.KilovoltagePeak:0.#} / mAs {session.ExposureParameters.MilliampereSeconds:0.#}";
        var artifactPath = Path.Combine("simulated-output", $"{imageId}.dcm");
        return Task.FromResult(new ExposureResult(imageId, previewText, artifactPath, DateTime.UtcNow));
    }
}