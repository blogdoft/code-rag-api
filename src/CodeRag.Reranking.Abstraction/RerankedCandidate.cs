namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// The outcome of reranking one candidate. <see cref="Score"/> is null when the candidate was
/// not actually scored (reranking disabled) - the containing list's order is still meaningful
/// in that case (it mirrors the original vector-search order).
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record RerankedCandidate(long Id, double? Score);
#pragma warning restore SA1313
