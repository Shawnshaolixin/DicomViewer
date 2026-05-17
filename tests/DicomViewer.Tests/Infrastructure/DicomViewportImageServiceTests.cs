using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Imaging;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace DicomViewer.Tests.Infrastructure;

public sealed class DicomViewportImageServiceTests
{
    [Fact]
    public void TryLoad_WithGrayscaleDicom_ReturnsViewportImage()
    {
        var service = new DicomViewportImageService();
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"dicomviewer-{Guid.NewGuid():N}.dcm");

        try
        {
            CreateTestDicom(tempFilePath);

            var image = service.TryLoad(tempFilePath, 0, new WindowLevel(255, 127.5));

            Assert.NotNull(image);
            Assert.Equal(2, image!.Width);
            Assert.Equal(2, image.Height);
            Assert.Equal(4, image.Pixels.Length);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void CreateTestDicom(string filePath)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.PatientName, "Render^Test" },
            { DicomTag.PatientID, "TEST-1" },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.StudyDescription, "Render Test Study" },
            { DicomTag.SeriesDescription, "Render Test Series" },
            { DicomTag.Modality, "OT" },
            { DicomTag.Rows, (ushort)2 },
            { DicomTag.Columns, (ushort)2 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
            { DicomTag.PhotometricInterpretation, "MONOCHROME2" },
            { DicomTag.WindowWidth, 255.0 },
            { DicomTag.WindowCenter, 127.5 },
        };

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(new byte[] { 0, 85, 170, 255 }));

        new DicomFile(dataset).Save(filePath);
    }
}