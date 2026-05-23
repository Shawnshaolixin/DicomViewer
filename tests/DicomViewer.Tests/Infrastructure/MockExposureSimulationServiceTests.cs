using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Dicom;
using DicomViewer.Infrastructure.Simulation;
using FellowOakDicom;

namespace DicomViewer.Tests.Infrastructure;

public sealed class MockExposureSimulationServiceTests
{
    [Fact]
    public async Task RunAsync_GeneratesReadableDicomArtifact()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();
        var builder = new SimulatedDicomBuilder(outputDirectory.FullName);
        var service = new MockExposureSimulationService(builder);
        var session = new ExamSession(
            "session-2",
            new ImagingOrder
            {
                OrderId = "ORD-2001",
                PatientId = "PAT-002",
                PatientName = "Exposure Patient",
                ProcedureDescription = "Knee AP",
                BodyPart = "KNEE",
                Projection = "AP",
                ScheduledTime = new DateTime(2026, 5, 23, 10, 0, 0, DateTimeKind.Local),
                Status = "Scheduled",
            },
            new ExposureParameters(72, 200, 10, 2.0, 1000, "KNEE", "AP", false),
            ExamWorkflowStatus.Acquiring,
            DeviceOperationalState.Exposing,
            new DateTime(2026, 5, 23, 2, 0, 0, DateTimeKind.Utc),
            null,
            null);

        try
        {
            var result = await service.RunAsync(session);

            Assert.True(File.Exists(result.ArtifactPath));
            Assert.Contains("封装为 DICOM", result.PreviewText, StringComparison.Ordinal);

            var dicomFile = await DicomFile.OpenAsync(result.ArtifactPath, FileReadOption.Default, 0);
            Assert.Equal("Exposure Patient", dicomFile.Dataset.GetSingleValue<string>(DicomTag.PatientName));
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }
}