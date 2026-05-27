using DicomViewer.Application.Models;
using DicomViewer.Domain.Entities;

namespace DicomViewer.Application.Abstractions;

public interface IMppsService
{
    Task<MppsSubmitResult> CreateInProgressAsync(ExamSession session, CancellationToken cancellationToken = default);

    Task<MppsSubmitResult> CompleteAsync(ExamSession session, CancellationToken cancellationToken = default);

    Task<MppsSubmitResult> DiscontinueAsync(ExamSession session, string reason, CancellationToken cancellationToken = default);
}