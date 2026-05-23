using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Abstractions;

/// <summary>
/// 定义曝光前联锁检查规则。
/// </summary>
public interface IInterlockService
{
    /// <summary>
    /// 对当前检查任务、参数和设备状态执行联锁校验。
    /// </summary>
    InterlockCheckResult Evaluate(
        ImagingOrder? order,
        ExposureParameters exposureParameters,
    ExposureParameterRange parameterRange,
        DeviceOperationalState deviceState,
        bool detectorConnected,
        bool tubeWarmedUp,
        bool doorClosed,
        bool pacsAvailable);
}