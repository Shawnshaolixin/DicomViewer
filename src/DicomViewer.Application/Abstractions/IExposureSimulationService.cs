using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IExposureSimulationService
{
    Task<ExposureResult> RunAsync(ExamSession session, string outputDirectory, CancellationToken cancellationToken = default);
}