using DicomViewer.Infrastructure.Data;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;

namespace DicomViewer.Tests.Infrastructure;

public sealed class FileSystemStudyCatalogServiceTests
{
    [Fact]
    public async Task LoadAsync_WithoutPath_ReturnsSampleStudy()
    {
        var service = new FileSystemStudyCatalogService();

        var result = await service.LoadAsync();

        Assert.NotEmpty(result.Patients);
        Assert.NotEmpty(result.Patients[0].Studies);
        Assert.NotEmpty(result.Patients[0].Studies[0].SeriesList);
        Assert.True(result.IsSampleData);
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFolder_ReturnsEmptyCollection()
    {
        var service = new FileSystemStudyCatalogService();
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            var result = await service.LoadAsync(directory.FullName);

            Assert.Empty(result.Patients);
            Assert.Equal("No DICOM series found", result.StatusText);
            Assert.Equal($"目录中未找到可解析的 DICOM 文件: {directory.FullName}", result.NoteText);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithDicomFiles_ReturnsImportStatistics()
    {
        var service = new FileSystemStudyCatalogService();
        var directory = Directory.CreateTempSubdirectory();
        var dicomFilePath = Path.Combine(directory.FullName, "study.dcm");
        var textFilePath = Path.Combine(directory.FullName, "notes.txt");

        try
        {
            CreateTestDicom(dicomFilePath);
            await File.WriteAllTextAsync(textFilePath, "not a dicom");

            var result = await service.LoadAsync(directory.FullName);

            Assert.Single(result.Patients);
            Assert.Equal("DICOM metadata imported", result.StatusText);
            Assert.Equal(2, result.ScannedFileCount);
            Assert.Equal(1, result.ImportedFileCount);
            Assert.Equal(1, result.SkippedFileCount);
            Assert.Equal($"已从目录加载 1 个实例，跳过 1 个无法解析的文件: {directory.FullName}", result.NoteText);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static void CreateTestDicom(string filePath)
    {
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.PatientName, "Catalog^Test" },
            { DicomTag.PatientID, "CATALOG-1" },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() },
            { DicomTag.StudyDescription, "Catalog Test Study" },
            { DicomTag.SeriesDescription, "Catalog Test Series" },
            { DicomTag.Modality, "CT" },
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

        DicomPixelData.Create(dataset, true).AddFrame(new MemoryByteBuffer(new byte[] { 0, 10, 20, 30 }));
        new DicomFile(dataset).Save(filePath);
    }
}