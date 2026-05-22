using DicomViewer.Infrastructure.Data;

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
            Assert.Contains("未找到可解析的 DICOM 文件", result.NoteText);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}