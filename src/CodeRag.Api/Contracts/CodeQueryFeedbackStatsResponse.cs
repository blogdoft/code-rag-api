namespace CodeRag.Api.Contracts;

/// <summary>
/// Feedback effectiveness statistics for a time window, as a dense week × project grid.
/// Serializes as snake_case.
/// </summary>
/// <param name="StartDate">Effective inclusive lower bound (UTC) of the aggregated window.</param>
/// <param name="EndDate">Effective inclusive upper bound (UTC) of the aggregated window.</param>
/// <param name="Weeks">
/// Every ISO calendar week (Monday-Sunday) overlapping the effective window, ordered by
/// <c>week_start</c> ascending. Always includes every overlapping week, even ones with zero
/// feedback across all projects.
/// </param>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryFeedbackStatsResponse(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<WeeklyFeedbackStatsResponse> Weeks);
#pragma warning restore SA1313
