using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IWorklistService
{
    Task<IReadOnlyList<ImagingOrder>> LoadAsync(CancellationToken cancellationToken = default);
}