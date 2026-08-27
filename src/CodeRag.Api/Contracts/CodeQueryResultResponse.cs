namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>CodeQueryResult</c> schema. Serializes as snake_case.</summary>
public sealed record CodeQueryResultResponse(
    long id,
    string? sourceFile,
    string kind,
    string? typeName,
    string? member,
    string embeddingText,
    double similarity);
