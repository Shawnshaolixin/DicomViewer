namespace DicomViewer.Domain.ValueObjects;

public sealed record ExposureParameterRange(
    double MinKilovoltagePeak,
    double MaxKilovoltagePeak,
    double MinTubeCurrentMilliampere,
    double MaxTubeCurrentMilliampere,
    double MinExposureTimeMilliseconds,
    double MaxExposureTimeMilliseconds,
    double MinMilliampereSeconds,
    double MaxMilliampereSeconds,
    double MinSourceToImageDistanceMillimeter,
    double MaxSourceToImageDistanceMillimeter)
{
    public static ExposureParameterRange Default { get; } = new(
        40,
        150,
        10,
        500,
        1,
        1000,
        0.1,
        500,
        500,
        2000);
}