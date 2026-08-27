namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>Project</c> schema. Serializes as snake_case.</summary>
public sealed record ProjectResponse(long id, string name, DateTime createdAt);
