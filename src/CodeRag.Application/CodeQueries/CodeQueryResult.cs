namespace CodeRag.Application.CodeQueries;

/// <summary>
/// A single code document matched against a natural language query, sourced from
/// <c>public.code_documents</c>, annotated with its computed cosine similarity.
/// </summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryResult(
    long Id,
    string? SourceFile,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity,
    string? GitUrl = null,
    string? GitRawUrl = null,
    double? RerankScore = null);
#pragma warning restore SA1313
