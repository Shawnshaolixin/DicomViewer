using System.Text.Json;
using DicomViewer.Application.Abstractions;
using DicomViewer.Application.Models;

namespace DicomViewer.Infrastructure.Persistence;

public sealed class JsonConsoleConfigurationStore : IConsoleConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _filePath;

    public JsonConsoleConfigurationStore(string? filePath = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DicomViewer",
                "console-settings.json")
            : filePath;
    }

    public ConsoleConfiguration Load()
    {
        if (!File.Exists(_filePath))
        {
            return ConsoleConfiguration.Default;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ConsoleConfiguration>(json, SerializerOptions) ?? ConsoleConfiguration.Default;
        }
        catch
        {
            return ConsoleConfiguration.Default;
        }
    }

    public void Save(ConsoleConfiguration configuration)
    {
        var directoryPath = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(configuration, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }
}