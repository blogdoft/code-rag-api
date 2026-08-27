using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>CodeQueryRequest</c> schema.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryRequest(string? Question);
#pragma warning restore SA1313
