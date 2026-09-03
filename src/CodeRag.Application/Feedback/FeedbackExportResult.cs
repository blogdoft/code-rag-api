namespace CodeRag.Application.Feedback;

/// <summary>
/// Raw feedback records for a time window, as a flat list ordered by <c>created_at</c> ascending.
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record FeedbackExportResult(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyList<FeedbackExportRow> Rows);
#pragma warning restore SA1313
