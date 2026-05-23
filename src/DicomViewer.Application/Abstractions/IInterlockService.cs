using DicomViewer.Domain.Entities;
using DicomViewer.Domain.Enums;
using DicomViewer.Domain.ValueObjects;

namespace DicomViewer.Application.Abstractions;

public interface IInterlockService
{
    InterlockCheckResult Evaluate(
        ImagingOrder? order,
        ExposureParameters exposureParameters,
        DeviceOperationalState deviceState,
        bool detectorConnected,
        bool tubeWarmedUp,
        bool doorClosed,
        bool pacsAvailable);
}