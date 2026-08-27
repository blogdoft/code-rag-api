namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="CodeQueryTools.QueryProjectCodeAsync"/> to MCP clients.</summary>
public sealed record CodeQueryToolResult(
    long id,
    string? sourceFile,
    string kind,
    string? typeName,
    string? member,
    string embeddingText,
    double similarity);
