namespace CodeRag.Application.CodeQueries;

/// <summary>
/// A single code document matched against a natural language query, sourced from
/// <c>public.code_documents</c>, annotated with its computed cosine similarity.
/// </summary>
public sealed record CodeQueryResult(
    long Id,
    string? SourceFile,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity);
