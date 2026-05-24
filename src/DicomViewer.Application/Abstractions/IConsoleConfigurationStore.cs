using DicomViewer.Application.Models;

namespace DicomViewer.Application.Abstractions;

public interface IConsoleConfigurationStore
{
    ConsoleConfiguration Load();

    void Save(ConsoleConfiguration configuration);
}