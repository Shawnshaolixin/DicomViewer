using DicomViewer.Domain.Enums;

namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// 表示一次联锁检查的通过状态、失败码和提示消息。
/// </summary>
public sealed record InterlockCheckResult(
    bool IsPassed,
    IReadOnlyList<InterlockCode> FailedCodes,
    IReadOnlyList<string> Messages)
{
    /// <summary>
    /// 表示无任何联锁失败项的结果。
    /// </summary>
    public static InterlockCheckResult Passed { get; } = new(true, Array.Empty<InterlockCode>(), Array.Empty<string>());

    /// <summary>
    /// 根据失败列表构造未通过的联锁结果。
    /// </summary>
    public static InterlockCheckResult Fail(params (InterlockCode Code, string Message)[] failures)
    {
        return new InterlockCheckResult(
            false,
            failures.Select(item => item.Code).ToArray(),
            failures.Select(item => item.Message).ToArray());
    }
}