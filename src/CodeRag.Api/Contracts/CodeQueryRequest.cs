using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Natural language search request scoped to a single project.</summary>
/// <param name="Question">
/// A natural language ask describing the code the caller is looking for (e.g. "where is the
/// retry logic for failed payments?"). This text is embedded and compared against the stored
/// code documents' embeddings for the target project using cosine similarity. Must not be empty
/// or blank.
/// </param>
/// <param name="Kind">Optional filter narrowing results by <c>kind</c>. Omit for no filtering on this field.</param>
/// <param name="Namespace">Optional filter narrowing results by <c>namespace</c>. Omit for no filtering on this field.</param>
/// <param name="TypeName">Optional filter narrowing results by <c>typeName</c>. Omit for no filtering on this field.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryRequest(
    string? Question,
    CodeQueryKindFilterRequest? Kind = null,
    CodeQueryNamespaceFilterRequest? Namespace = null,
    CodeQueryTypeNameFilterRequest? TypeName = null);
#pragma warning restore SA1313
