using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>CodeQueryRequest</c> schema.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeQueryRequest(string? question);
