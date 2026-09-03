namespace CodeRag.Application.Feedback;

/// <summary>
/// Feedback effectiveness statistics for a single ISO calendar week (Monday-Sunday), broken down
/// by project. Every registered project (or a single filtered project) is always present, even
/// with zero feedback in this specific week.
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record WeeklyFeedbackStats(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<ProjectFeedbackStats> Projects);
#pragma warning restore SA1313
