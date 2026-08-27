namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="ProjectTools.ListProjectsAsync"/> to MCP clients.</summary>
public sealed record ProjectToolResult(long id, string name, DateTime createdAt);
