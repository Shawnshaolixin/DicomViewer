namespace DicomViewer.Domain.ValueObjects;

public sealed record ExposureParameters(
    double KilovoltagePeak,
    double TubeCurrentMilliampere,
    double ExposureTimeMilliseconds,
    double MilliampereSeconds,
    double SourceToImageDistanceMillimeter,
    string BodyPart,
    string Projection,
    bool IsAutomaticExposureControlEnabled)
{
    public static ExposureParameters Default { get; } = new(
        70,
        250,
        12,
        3,
        1000,
        "CHEST",
        "PA",
        false);
}