namespace CodeRag.Api.Contracts;

/// <summary>
/// A recorded piece of feedback on a prior code query. Serializes as snake_case. There is no GET
/// endpoint for retrieving it later in this iteration - this response is the only view of it.
/// </summary>
/// <param name="Id">Auto-generated, unique identifier of the feedback record.</param>
/// <param name="ProjectId">Id of the project the original code query was scoped to.</param>
/// <param name="Question">The natural language question that was originally sent to <c>POST .../code-queries</c>.</param>
/// <param name="Useful">Whether the results returned for <c>question</c> were useful to the caller.</param>
/// <param name="Similarities">The exact <c>similarity</c> values returned by the original code-queries call, as submitted.</param>
/// <param name="Reason">Free-text explanation of why the results were not useful, if provided.</param>
/// <param name="User">Identity of the caller who submitted this feedback (human username, or MCP agent/tool name).</param>
/// <param name="CreatedAt">Timestamp (UTC) at which the feedback record was created.</param>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryFeedbackResponse(
    long Id,
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
