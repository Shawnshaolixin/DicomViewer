using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IStudyCatalogService
{
    Task<StudyCatalogLoadResult> LoadAsync(string? sourcePath = null, CancellationToken cancellationToken = default);
}