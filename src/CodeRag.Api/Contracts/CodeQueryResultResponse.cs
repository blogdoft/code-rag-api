namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>CodeQueryResult</c> schema. Serializes as snake_case.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryResultResponse(
    long Id,
    string? SourceFile,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity);
#pragma warning restore SA1313
