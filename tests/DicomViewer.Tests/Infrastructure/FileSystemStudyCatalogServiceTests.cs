using DicomViewer.Infrastructure.Data;

namespace DicomViewer.Tests.Infrastructure;

public sealed class FileSystemStudyCatalogServiceTests
{
    [Fact]
    public async Task LoadAsync_WithoutPath_ReturnsSampleStudy()
    {
        var service = new FileSystemStudyCatalogService();

        var patients = await service.LoadAsync();

        Assert.NotEmpty(patients);
        Assert.NotEmpty(patients[0].Studies);
        Assert.NotEmpty(patients[0].Studies[0].SeriesList);
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFolder_ReturnsEmptyCollection()
    {
        var service = new FileSystemStudyCatalogService();
        var directory = Directory.CreateTempSubdirectory();

        try
        {
            var patients = await service.LoadAsync(directory.FullName);

            Assert.Empty(patients);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}