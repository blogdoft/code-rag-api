namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>CodeQueryResult</c> schema. Serializes as snake_case.</summary>
public sealed record CodeQueryResultResponse(
    long Id,
    string? SourceFile,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity);
