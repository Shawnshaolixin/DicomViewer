using DicomViewer.Application.Models;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Persistence;

namespace DicomViewer.Tests.Infrastructure;

public sealed class SqliteConsoleConfigurationStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsConsoleConfiguration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var databasePath = Path.Combine(tempDirectory.FullName, "dicomviewer.db");
        var connectionFactory = new SqliteAppDbConnectionFactory(databasePath);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);
        var store = new SqliteConsoleConfigurationStore(connectionFactory);
        var configuration = new ConsoleConfiguration(
            new PacsConfiguration("LOCALAE", "ORTHANC", "127.0.0.1", 4242, 8042, @"D:\DicomOutput", "127.0.0.1", 11113),
            new ExposureParameterRange(45, 130, 15, 400, 1, 800, 0.2, 200, 600, 1800));

        try
        {
            databaseInitializer.EnsureCreated();
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
    public void Load_WhenDatabaseHasNoConfiguration_ReturnsDefaultConfiguration()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        var databasePath = Path.Combine(tempDirectory.FullName, "dicomviewer.db");
        var connectionFactory = new SqliteAppDbConnectionFactory(databasePath);
        var databaseInitializer = new SqliteDatabaseInitializer(connectionFactory);
        var store = new SqliteConsoleConfigurationStore(connectionFactory);

        try
        {
            databaseInitializer.EnsureCreated();

            var loaded = store.Load();

            Assert.Equal(ConsoleConfiguration.Default, loaded);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }
}