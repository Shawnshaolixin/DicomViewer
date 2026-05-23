using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Dicom;
using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace DicomViewer.Tests.Infrastructure;

public sealed class SimulatedDicomBuilderTests
{
    [Fact]
    public async Task BuildAsync_WritesDicomFileWithExpectedMetadataAndPixels()
    {
        var outputDirectory = Directory.CreateTempSubdirectory();
        var builder = new SimulatedDicomBuilder(outputDirectory.FullName);
        var session = new ExamSession(
            "session-1",
            new ImagingOrder
            {
                OrderId = "ORD-1001",
                PatientId = "PAT-001",
                PatientName = "Demo Patient",
                ProcedureDescription = "Chest PA",
                BodyPart = "CHEST",
                Projection = "PA",
                ScheduledTime = new DateTime(2026, 5, 23, 9, 0, 0, DateTimeKind.Local),
                Status = "Scheduled",
            },
            new ExposureParameters(80, 320, 16, 5.1, 1100, "CHEST", "PA", false),
            ExamWorkflowStatus.Processing,
            DeviceOperationalState.Processing,
            new DateTime(2026, 5, 23, 1, 0, 0, DateTimeKind.Utc),
            null,
            null);
        var acquiredAtUtc = new DateTime(2026, 5, 23, 1, 5, 0, DateTimeKind.Utc);

        try
        {
            var filePath = await builder.BuildAsync(session, "SIM-TEST-1", acquiredAtUtc);

            Assert.True(File.Exists(filePath));

            var dicomFile = await DicomFile.OpenAsync(filePath, FileReadOption.Default, 0);
            var dataset = dicomFile.Dataset;

            Assert.Equal("Demo Patient", dataset.GetSingleValue<string>(DicomTag.PatientName));
            Assert.Equal("PAT-001", dataset.GetSingleValue<string>(DicomTag.PatientID));
            Assert.Equal("DX", dataset.GetSingleValue<string>(DicomTag.Modality));
            Assert.Equal("CHEST", dataset.GetSingleValue<string>(DicomTag.BodyPartExamined));
            Assert.Equal("PA", dataset.GetSingleValue<string>(DicomTag.ViewPosition));
            Assert.Equal((ushort)256, dataset.GetSingleValue<ushort>(DicomTag.Rows));
            Assert.Equal((ushort)256, dataset.GetSingleValue<ushort>(DicomTag.Columns));
            Assert.Equal(255.0, dataset.GetSingleValue<double>(DicomTag.WindowWidth));
            Assert.Equal(127.5, dataset.GetSingleValue<double>(DicomTag.WindowCenter));
            Assert.Equal(80.0, dataset.GetSingleValue<double>(DicomTag.KVP));

            var pixelData = DicomPixelData.Create(dataset);
            Assert.Equal(1, pixelData.NumberOfFrames);
            Assert.Equal(65536, pixelData.GetFrame(0).Size);
        }
        finally
        {
            outputDirectory.Delete(true);
        }
    }
}