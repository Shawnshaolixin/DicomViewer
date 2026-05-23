namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示一次曝光所需的核心技术参数。
/// </summary>
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
    /// <summary>
    /// 控制台用于初始化界面的默认曝光参数。
    /// </summary>
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