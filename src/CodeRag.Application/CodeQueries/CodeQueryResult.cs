namespace CodeRag.Application.CodeQueries;

/// <summary>
/// A single code document matched against a natural language query, sourced from
/// <c>public.code_documents</c>, annotated with its computed cosine similarity.
/// </summary>
public sealed record CodeQueryResult(
    long id,
    string? sourceFile,
    string kind,
    string? typeName,
    string? member,
    string embeddingText,
    double similarity);
