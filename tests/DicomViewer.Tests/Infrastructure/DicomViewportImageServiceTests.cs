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

            Assert.True(image.Succeeded);
            Assert.NotNull(image.Image);
            Assert.Equal(2, image.Image!.Width);
            Assert.Equal(2, image.Image.Height);
            Assert.Equal(4, image.Image.Pixels.Length);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public void TryLoad_WithDifferentWindowLevels_ChangesPixelMapping()
    {
        var service = new DicomViewportImageService();
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"dicomviewer-{Guid.NewGuid():N}.dcm");

        try
        {
            CreateTestDicom(tempFilePath);

            var narrowWindow = service.TryLoad(tempFilePath, 0, new WindowLevel(64, 32));
            var wideWindow = service.TryLoad(tempFilePath, 0, new WindowLevel(255, 127.5));

            Assert.True(narrowWindow.Succeeded);
            Assert.True(wideWindow.Succeeded);
            Assert.NotEqual(narrowWindow.Image!.Pixels[1], wideWindow.Image!.Pixels[1]);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public void TryLoad_WithMultiFrameDicom_CanLoadRequestedFrame()
    {
        var service = new DicomViewportImageService();
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"dicomviewer-{Guid.NewGuid():N}.dcm");

        try
        {
            CreateTestDicom(tempFilePath, framePixels: new[]
            {
                new byte[] { 0, 10, 20, 30 },
                new byte[] { 200, 210, 220, 230 },
            });

            var firstFrame = service.TryLoad(tempFilePath, 0, new WindowLevel(255, 127.5));
            var secondFrame = service.TryLoad(tempFilePath, 1, new WindowLevel(255, 127.5));

            Assert.True(firstFrame.Succeeded);
            Assert.True(secondFrame.Succeeded);
            Assert.NotEqual(firstFrame.Image!.Pixels[0], secondFrame.Image!.Pixels[0]);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void CreateTestDicom(string filePath, IReadOnlyList<byte[]>? framePixels = null)
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

        var pixelData = DicomPixelData.Create(dataset, true);
        var frames = framePixels ?? new[] { new byte[] { 0, 85, 170, 255 } };
        dataset.AddOrUpdate(DicomTag.NumberOfFrames, frames.Count);
        foreach (var frame in frames)
        {
            pixelData.AddFrame(new MemoryByteBuffer(frame));
        }

        new DicomFile(dataset).Save(filePath);
    }
}