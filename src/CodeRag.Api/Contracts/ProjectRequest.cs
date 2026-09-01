using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the request body for creating or updating a <c>Project</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record ProjectRequest(string? Name, string? GitUrl, string? GitRawUrl);
#pragma warning restore SA1313
