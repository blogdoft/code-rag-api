namespace CodeRag.Application.Feedback;

/// <summary>A recorded piece of feedback on a prior code query. Maps to <c>public.code_query_feedback</c>.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record FeedbackResult(
    long Id,
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
