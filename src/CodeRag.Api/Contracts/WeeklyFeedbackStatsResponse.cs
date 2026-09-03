namespace CodeRag.Api.Contracts;

/// <summary>Feedback effectiveness statistics for a single ISO calendar week (Monday-Sunday).</summary>
/// <param name="WeekStart">Monday of this ISO calendar week.</param>
/// <param name="WeekEnd">Sunday of this ISO calendar week.</param>
/// <param name="Projects">
/// Every registered project (or the single project matching <c>project_id</c>, if given), ordered
/// by <c>project_id</c>, with counts/percentages for feedback created within this week and within
/// the overall requested window. Zero-filled when a project had no feedback in this specific week.
/// </param>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record WeeklyFeedbackStatsResponse(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<ProjectFeedbackStatsResponse> Projects);
#pragma warning restore SA1313
