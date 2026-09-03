namespace CodeRag.Application.Feedback;

/// <summary>
/// Feedback effectiveness statistics for a time window, as a dense week × project grid. Every ISO
/// calendar week overlapping the window is present, ordered by <see cref="WeeklyFeedbackStats.WeekStart"/>.
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record FeedbackStatsResult(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<WeeklyFeedbackStats> Weeks);
#pragma warning restore SA1313
