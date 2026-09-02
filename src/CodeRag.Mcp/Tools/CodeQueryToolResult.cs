namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="CodeQueryTools.QueryProjectCodeAsync"/> to MCP clients.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryToolResult(
    long Id,
    string? SourceFile,
    string? GitRawUrl,
    string? GitUrl,
    string Kind,
    string? TypeName,
    string? Member,
    string EmbeddingText,
    double Similarity,
    double? RerankScore);
#pragma warning restore SA1313
