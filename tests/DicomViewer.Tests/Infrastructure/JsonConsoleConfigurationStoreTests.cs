using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;

namespace DicomViewer.Tests.Infrastructure;

public sealed class JsonConsoleConfigurationStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsConsoleConfiguration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tempDirectory.FullName, "console-settings.json");
        var store = new JsonConsoleConfigurationStore(filePath);
        var configuration = new ConsoleConfiguration(
            new PacsConfiguration("LOCALAE", "ORTHANC", "127.0.0.1", 4242, @"D:\DicomOutput"),
            new ExposureParameterRange(45, 130, 15, 400, 1, 800, 0.2, 200, 600, 1800));

        try
        {
            store.Save(configuration);

            var loaded = store.Load();

            Assert.Equal(configuration, loaded);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultConfiguration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var filePath = Path.Combine(tempDirectory.FullName, "missing.json");
        var store = new JsonConsoleConfigurationStore(filePath);

        try
        {
            var loaded = store.Load();

            Assert.Equal(ConsoleConfiguration.Default, loaded);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}