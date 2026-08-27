namespace CodeRag.Application.Projects;

/// <summary>A registered project whose source code has been indexed. Maps to <c>public.projects</c>.</summary>
public sealed record Project(long id, string name, DateTime createdAt);
