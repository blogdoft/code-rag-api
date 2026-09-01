using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>
/// A single code document matched against a natural language query, annotated with its computed
/// similarity score. Serializes as snake_case, except <c>gitRawUrl</c>/<c>gitUrl</c> which are
/// camelCase per the OpenAPI contract.
/// </summary>
/// <param name="Id">Unique identifier of the matched code document.</param>
/// <param name="SourceFile">
/// Path to the source file this code document was extracted from, relative to the project root.
/// </param>
/// <param name="GitRawUrl">
/// Hyperlink to a raw view of this code document's source file, built by concatenating the
/// project's <c>git_raw_url</c> with <c>source_file</c>. Null when the project has no
/// <c>git_raw_url</c> set, or when <c>source_file</c> is null.
/// </param>
/// <param name="GitUrl">Hyperlink to the project's git repository (the project's <c>git_url</c>).</param>
/// <param name="Kind">
/// The kind of code element this document represents (e.g. <c>function</c>, <c>method</c>,
/// <c>class</c>, <c>interface</c>) as determined by the analyzer that indexed the source.
/// </param>
/// <param name="TypeName">
/// Name of the enclosing type (class, struct, interface, etc.), if any, that this document
/// belongs to.
/// </param>
/// <param name="Member">
/// Name of the specific member (method, property, field, function) this document represents, if
/// applicable.
/// </param>
/// <param name="EmbeddingText">
/// The normalized text representation of this code element that was embedded and stored as a
/// vector. Typically a signature, summary, or snippet describing the code element's purpose.
/// </param>
/// <param name="Similarity">
/// Cosine similarity between the query's embedding vector and this document's stored embedding.
/// Ranges from 1.0 (identical) to -1.0 (opposite); higher values indicate closer semantic matches.
/// </param>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryResultResponse(
    long Id,
    string? SourceFile,
    [property: JsonPropertyName("gitRawUrl")] string? GitRawUrl,
    [property: JsonPropertyName("gitUrl")] string? GitUrl,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity);
#pragma warning restore SA1313
