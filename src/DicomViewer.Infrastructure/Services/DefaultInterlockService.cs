using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Infrastructure.Services;

/// <summary>
/// 提供默认的曝光联锁规则实现。
/// 它将检查任务、设备状态、PACS 可用性和参数范围统一纳入校验。
/// </summary>
public sealed class DefaultInterlockService : IInterlockService
{
    /// <summary>
    /// 根据当前输入条件收集所有联锁失败项，并返回最终结果。
    /// </summary>
    public InterlockCheckResult Evaluate(
        ImagingOrder? order,
        ExposureParameters exposureParameters,
        ExposureParameterRange parameterRange,
        DeviceOperationalState deviceState,
        bool detectorConnected,
        bool tubeWarmedUp,
        bool doorClosed,
        bool pacsAvailable)
    {
        var failures = new List<(InterlockCode Code, string Message)>();

        if (order is null)
        {
            failures.Add((InterlockCode.NoActiveOrder, "未选择检查任务。"));
        }

        if (!detectorConnected)
        {
            failures.Add((InterlockCode.DetectorDisconnected, "探测器未连接。"));
        }

        if (!tubeWarmedUp)
        {
            failures.Add((InterlockCode.TubeNotWarmedUp, "球管未完成预热。"));
        }

        if (!doorClosed)
        {
            failures.Add((InterlockCode.DoorOpen, "防护门未关闭。"));
        }

        if (!pacsAvailable)
        {
            failures.Add((InterlockCode.PacsUnavailable, "PACS 服务不可用。"));
        }

        if (!IsInRange(exposureParameters, parameterRange))
        {
            failures.Add((InterlockCode.ParameterOutOfRange, "曝光参数越界。"));
        }

        return failures.Count == 0 ? InterlockCheckResult.Passed : InterlockCheckResult.Fail(failures.ToArray());
    }

    /// <summary>
    /// 判断曝光参数是否全部落在允许范围内，并包含必要的解剖部位与投照方向信息。
    /// </summary>
    private static bool IsInRange(ExposureParameters exposureParameters, ExposureParameterRange parameterRange)
    {
        return exposureParameters.KilovoltagePeak >= parameterRange.MinKilovoltagePeak
            && exposureParameters.KilovoltagePeak <= parameterRange.MaxKilovoltagePeak
            && exposureParameters.TubeCurrentMilliampere >= parameterRange.MinTubeCurrentMilliampere
            && exposureParameters.TubeCurrentMilliampere <= parameterRange.MaxTubeCurrentMilliampere
            && exposureParameters.ExposureTimeMilliseconds >= parameterRange.MinExposureTimeMilliseconds
            && exposureParameters.ExposureTimeMilliseconds <= parameterRange.MaxExposureTimeMilliseconds
            && exposureParameters.MilliampereSeconds >= parameterRange.MinMilliampereSeconds
            && exposureParameters.MilliampereSeconds <= parameterRange.MaxMilliampereSeconds
            && exposureParameters.SourceToImageDistanceMillimeter >= parameterRange.MinSourceToImageDistanceMillimeter
            && exposureParameters.SourceToImageDistanceMillimeter <= parameterRange.MaxSourceToImageDistanceMillimeter
            && !string.IsNullOrWhiteSpace(exposureParameters.BodyPart)
            && !string.IsNullOrWhiteSpace(exposureParameters.Projection);
    }
}