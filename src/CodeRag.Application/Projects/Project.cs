namespace CodeRag.Application.Projects;

/// <summary>A registered project whose source code has been indexed. Maps to <c>public.projects</c>.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record Project(long Id, string Name, string? GitUrl, string? GitRawUrl, DateTime CreatedAt);
#pragma warning restore SA1313
