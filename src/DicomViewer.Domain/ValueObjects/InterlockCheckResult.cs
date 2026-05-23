using DicomViewer.Domain.Enums;

namespace DicomViewer.Domain.ValueObjects;

public sealed record InterlockCheckResult(
    bool IsPassed,
    IReadOnlyList<InterlockCode> FailedCodes,
    IReadOnlyList<string> Messages)
{
    public static InterlockCheckResult Passed { get; } = new(true, Array.Empty<InterlockCode>(), Array.Empty<string>());

    public static InterlockCheckResult Fail(params (InterlockCode Code, string Message)[] failures)
    {
        return new InterlockCheckResult(
            false,
            failures.Select(item => item.Code).ToArray(),
            failures.Select(item => item.Message).ToArray());
    }
}