namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="CodeQueryTools.QueryProjectCode"/> to MCP clients.</summary>
public sealed record CodeQueryToolResult(
    long Id,
    string? SourceFile,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity);
