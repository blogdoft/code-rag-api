using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Feedback;

public interface IFeedbackService
{
    /// <summary>
    /// Records whether a prior code query's results were useful, so effectiveness can be
    /// measured later against the database.
    /// </summary>
    /// <param name="projectId">Id of the project the original code query was scoped to.</param>
    /// <param name="question">The natural language question that was originally sent to code-queries.</param>
    /// <param name="useful">Whether the results returned for <paramref name="question"/> were useful.</param>
    /// <param name="similarities">
    /// The exact <c>similarity</c> values (not <c>rerankScore</c>) returned by the original
    /// code-queries call. May be empty when the query returned zero results.
    /// </param>
    /// <param name="reason">Optional free-text explanation of why the results were not useful.</param>
    /// <param name="user">
    /// Identity of the caller submitting this feedback - a human's own identifier for REST
    /// callers, or the calling agent/tool's own name (e.g. "claude code", "codex") for MCP callers.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    Task<Result<FeedbackResult>> SubmitAsync(
        long projectId,
        string? question,
        bool? useful,
        IReadOnlyList<double>? similarities,
        string? reason,
        string? user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns feedback effectiveness statistics for a time window, as a dense week × project
    /// grid - see the "Desenho do contrato" section of <c>.specs/code-query-feedback-stats.md</c>
    /// for the full default-window and validation rules.
    /// </summary>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC) of the window. <c>null</c> is treated per the default-window
    /// rules together with <paramref name="endDate"/>.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC) of the window. <c>null</c> is treated per the default-window
    /// rules together with <paramref name="startDate"/>.
    /// </param>
    /// <param name="projectId">
    /// When given, restricts every week's project list to this single project; a non-existent
    /// project results in a 404 failure.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    Task<Result<FeedbackStatsResult>> GetStatsAsync(
        DateTime? startDate,
        DateTime? endDate,
        long? projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every raw feedback record for a time window, as a flat list ordered by
    /// <c>created_at</c> ascending - see the "Desenho do contrato" section of
    /// <c>.specs/code-query-feedback-export.md</c> for the full default-window and validation
    /// rules.
    /// </summary>
    /// <param name="startDate">
    /// Inclusive lower bound (UTC) of the window. <c>null</c> defaults to the first day of the
    /// current UTC month, independently of <paramref name="endDate"/>.
    /// </param>
    /// <param name="endDate">
    /// Inclusive upper bound (UTC) of the window. <c>null</c> defaults to now (UTC),
    /// independently of <paramref name="startDate"/>.
    /// </param>
    /// <param name="projectId">
    /// When given, restricts the export to this single project; a non-existent project results
    /// in a 404 failure.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    Task<Result<FeedbackExportResult>> ExportAsync(
        DateTime? startDate,
        DateTime? endDate,
        long? projectId,
        CancellationToken cancellationToken = default);
}
