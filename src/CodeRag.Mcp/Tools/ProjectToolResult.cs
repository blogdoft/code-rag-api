namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="ProjectTools.ListProjectsAsync"/> to MCP clients.</summary>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectToolResult(long Id, string Name, DateTime CreatedAt);
#pragma warning restore SA1313
