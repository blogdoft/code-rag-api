namespace CodeRag.Mcp.Tools;

/// <summary>Wire shape returned by <see cref="CodeQueryTools.SubmitCodeQueryFeedbackAsync"/> to MCP clients.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record CodeQueryFeedbackToolResult(
    long Id,
    long ProjectId,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string User,
    DateTime CreatedAt);
#pragma warning restore SA1313
