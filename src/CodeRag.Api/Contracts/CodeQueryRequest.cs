using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Natural language search request scoped to a single project.</summary>
/// <param name="Question">
/// A natural language ask describing the code the caller is looking for (e.g. "where is the
/// retry logic for failed payments?"). This text is embedded and compared against the stored
/// code documents' embeddings for the target project using cosine similarity. Must not be empty
/// or blank.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryRequest(string? Question);
#pragma warning restore SA1313
