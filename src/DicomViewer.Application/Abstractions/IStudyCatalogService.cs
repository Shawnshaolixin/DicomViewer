using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IStudyCatalogService
{
    Task<IReadOnlyList<Patient>> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default);
}