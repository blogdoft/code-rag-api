using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.CodeQueries;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CodeRag.Mcp.Tools;

/// <summary>MCP tools for researching a project's indexed source code, backed directly by the Application layer.</summary>
[McpServerToolType]
public sealed class CodeQueryTools(ICodeQueryService codeQueryService)
{
    [McpServerTool(Name = "query_project_code", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Searches a project's indexed source code using a natural language question and returns the code " +
        "documents (functions, methods, types, etc.) whose embeddings are most semantically similar, ordered " +
        "by descending similarity (1.0 = identical, values near or below 0 = unrelated). Use list_projects " +
        "first to find the projectId.")]
    public async Task<IEnumerable<CodeQueryToolResult>> QueryProjectCodeAsync(
        [Description("Id of the project to search, from the list_projects tool.")]
        long projectId,
        [Description(
            "A natural language description of the code you are looking for, " +
            "e.g. 'where is the retry logic for failed payments?'.")]
        string question,
        [Description(
            "Maximum number of results to return, between 1 and 50. Defaults to 10 when omitted.")]
        int? limit = null,
        [Description(
            "Minimum cosine similarity (0.0 to 1.0) a result must have to be included. Results below " +
            "this are semantically unrelated noise rather than genuine matches - set e.g. 0.5 to filter " +
            "them out. Omit to get the unfiltered top results regardless of relevance.")]
        double? minSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        var result = await codeQueryService.QueryAsync(projectId, question, limit, minSimilarity, cancellationToken);

        return result.Map(
            onSuccess: results => results.Select(ToToolResult),
            onFailure: failure => throw new McpException(failure.Message));
    }

    private static CodeQueryToolResult ToToolResult(CodeQueryResult result) => new(
        result.Id,
        result.SourceFile,
        result.GitRawUrl,
        result.GitUrl,
        result.Kind,
        result.TypeName,
        result.Member,
        result.EmbeddingText,
        result.Similarity);
}
