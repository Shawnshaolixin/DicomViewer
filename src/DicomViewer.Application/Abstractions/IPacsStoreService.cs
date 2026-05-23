using DicomViewer.Application.Models;

namespace DicomViewer.Application.Abstractions;

public interface IPacsStoreService
{
    Task<PacsStoreResult> VerifyConnectionAsync(PacsConfiguration configuration, CancellationToken cancellationToken = default);

    Task<PacsStoreResult> SendAsync(string dicomFilePath, PacsConfiguration configuration, CancellationToken cancellationToken = default);
}