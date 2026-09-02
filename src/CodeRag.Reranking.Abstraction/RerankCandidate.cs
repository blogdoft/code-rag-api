namespace CodeRag.Reranking.Abstraction;

/// <summary>A vector-search result offered up for reranking, identified by id with its embedded text.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record RerankCandidate(long Id, string Text);
#pragma warning restore SA1313
