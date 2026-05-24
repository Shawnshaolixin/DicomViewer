using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Models;

public sealed record ConsoleConfiguration(
    PacsConfiguration PacsConfiguration,
    ExposureParameterRange ExposureParameterRange)
{
    public static ConsoleConfiguration Default { get; } = new(
        PacsConfiguration.Default,
        ExposureParameterRange.Default);
}