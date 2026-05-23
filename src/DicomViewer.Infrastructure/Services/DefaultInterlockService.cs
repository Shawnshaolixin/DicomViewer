using DicomViewer.Application.Abstractions;
using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Infrastructure.Services;

public sealed class DefaultInterlockService : IInterlockService
{
    public InterlockCheckResult Evaluate(
        ImagingOrder? order,
        ExposureParameters exposureParameters,
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

        if (!IsInRange(exposureParameters))
        {
            failures.Add((InterlockCode.ParameterOutOfRange, "曝光参数越界。"));
        }

        return failures.Count == 0 ? InterlockCheckResult.Passed : InterlockCheckResult.Fail(failures.ToArray());
    }

    private static bool IsInRange(ExposureParameters exposureParameters)
    {
        return exposureParameters.KilovoltagePeak is >= 40 and <= 150
            && exposureParameters.TubeCurrentMilliampere is >= 10 and <= 500
            && exposureParameters.ExposureTimeMilliseconds is >= 1 and <= 1000
            && exposureParameters.MilliampereSeconds is >= 0.1 and <= 500
            && exposureParameters.SourceToImageDistanceMillimeter is >= 500 and <= 2000
            && !string.IsNullOrWhiteSpace(exposureParameters.BodyPart)
            && !string.IsNullOrWhiteSpace(exposureParameters.Projection);
    }
}