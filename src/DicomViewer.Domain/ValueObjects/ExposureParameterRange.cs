namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示联锁检查允许的曝光参数范围。
/// </summary>
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
    /// <summary>
    /// 控制台初始化时使用的默认参数范围。
    /// </summary>
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