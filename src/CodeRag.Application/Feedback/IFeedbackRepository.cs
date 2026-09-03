namespace CodeRag.Application.Feedback;

public interface IFeedbackRepository
{
    /// <summary>Inserts a new feedback record and returns the persisted row, including its generated id and created_at.</summary>
    /// <param name="projectId">Id of the project the original code query was scoped to.</param>
    /// <param name="question">The natural language question that was originally sent to code-queries.</param>
    /// <param name="useful">Whether the results returned for <paramref name="question"/> were useful.</param>
    /// <param name="similarities">The similarity values returned by the original code-queries call.</param>
    /// <param name="reason">Optional free-text explanation of why the results were not useful.</param>
    /// <param name="user">Identity of the caller submitting this feedback.</param>
    /// <param name="cancellationToken">Token used to cancel the insert.</param>
    Task<FeedbackResult> InsertAsync(
        long projectId,
        string question,
        bool useful,
        IReadOnlyList<double> similarities,
        string? reason,
        string user,
        CancellationToken cancellationToken = default);

    /// <summary>Whether any feedback record exists for the given project.</summary>
    /// <param name="projectId">Id of the project to check.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<bool> ExistsForProjectAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns feedback effectiveness statistics as a dense week × project grid: every ISO
    /// calendar week overlapping <paramref name="startDate"/>/<paramref name="endDate"/> is
    /// present, and within each week, every eligible project is present (even with zero feedback
    /// in that specific week).
    /// </summary>
    /// <param name="startDate">Inclusive lower bound (UTC) of the window.</param>
    /// <param name="endDate">Inclusive upper bound (UTC) of the window.</param>
    /// <param name="projectId">When given, restricts every week's project list to this single project.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<IReadOnlyList<WeeklyFeedbackStats>> GetStatsAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default);
}
