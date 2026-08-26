namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="ProjectTools.ListProjects"/> to MCP clients.</summary>
public sealed record ProjectToolResult(long Id, string Name, DateTime CreatedAt);
