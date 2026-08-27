namespace CodeRag.Api.Contracts;

/// <summary>Wire representation of the <c>Project</c> schema. Serializes as snake_case.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectResponse(long Id, string Name, DateTime CreatedAt);
#pragma warning restore SA1313
