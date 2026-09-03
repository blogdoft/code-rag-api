using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CodeRag.Mcp.Tools;

/// <summary>MCP tools for researching a project's indexed source code, backed directly by the Application layer.</summary>
[McpServerToolType]
public sealed class CodeQueryTools(ICodeQueryService codeQueryService, IFeedbackService feedbackService)
{
    [McpServerTool(Name = "query_project_code", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Searches a project's indexed source code using a natural language question and returns the code " +
        "documents (functions, methods, types, etc.) whose embeddings are most semantically similar, ordered " +
        "by descending relevance - by rerank score when reranking is enabled, otherwise by cosine similarity " +
        "(1.0 = identical, values near or below 0 = unrelated). Use list_projects first to find the projectId. " +
        "After reviewing the results, call submit_code_query_feedback to report whether they were useful.")]
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
        [Description(
            "Comparison operator for an optional filter on 'kind'. Must be set together with kindValue; " +
            "omit both for no filtering on this field.")]
        KindFilterOperator? kindOperator = null,
        [Description(
            "Value to compare 'kind' against using kindOperator. For the Contains operator, '*' acts as " +
            "a wildcard matching any sequence of characters (e.g. 'fun*'); a value with no '*' is matched " +
            "exactly (case-insensitively).")]
        string? kindValue = null,
        [Description(
            "Comparison operator for an optional filter on 'namespace'. Must be set together with " +
            "namespaceValue; omit both for no filtering on this field.")]
        NamespaceFilterOperator? namespaceOperator = null,
        [Description(
            "Value to compare 'namespace' against using namespaceOperator. For the Contains/NotContains " +
            "operators, '*' acts as a wildcard matching any sequence of characters (e.g. '*Billing*'); a " +
            "value with no '*' is matched exactly (case-insensitively).")]
        string? namespaceValue = null,
        [Description(
            "Comparison operator for an optional filter on 'typeName'. Must be set together with " +
            "typeNameValue; omit both for no filtering on this field.")]
        TypeNameFilterOperator? typeNameOperator = null,
        [Description(
            "Value to compare 'typeName' against using typeNameOperator. For the Contains/NotContains " +
            "operators, '*' acts as a wildcard matching any sequence of characters (e.g. '*Controller'); a " +
            "value with no '*' is matched exactly (case-insensitively).")]
        string? typeNameValue = null,
        CancellationToken cancellationToken = default)
    {
        var result = await codeQueryService.QueryAsync(
            projectId,
            question,
            limit,
            minSimilarity,
            kindOperator,
            kindValue,
            namespaceOperator,
            namespaceValue,
            typeNameOperator,
            typeNameValue,
            cancellationToken);

        return result.Map(
            onSuccess: results => results.Select(ToToolResult),
            onFailure: failure => throw new McpException(failure.Message));
    }

    [McpServerTool(Name = "submit_code_query_feedback", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description(
        "Records whether a prior query_project_code call's results were useful, so the effectiveness of RAG " +
        "questions can be measured. Call this after reviewing the results of query_project_code, passing back " +
        "the exact similarity values you received (not rerankScore). You MUST always identify yourself via the " +
        "user parameter with your own agent/tool name (e.g. 'claude code', 'codex', 'crewai', 'hermes', " +
        "'opencode') - never omit it or guess a value on the caller's behalf.")]
    public async Task<CodeQueryFeedbackToolResult> SubmitCodeQueryFeedbackAsync(
        [Description("Id of the project the original query_project_code call was scoped to.")]
        long projectId,
        [Description("The natural language question that was originally sent to query_project_code.")]
        string question,
        [Description("Whether the results returned for the question were useful.")]
        bool useful,
        [Description(
            "The exact similarity values (not rerankScore) returned by the original query_project_code call, " +
            "in the order they were received. Pass an empty array when the query returned zero results.")]
        IReadOnlyList<double> similarities,
        [Description(
            "Your own identity as the calling agent/tool, e.g. 'claude code', 'codex', 'crewai', 'hermes', " +
            "'opencode'. Required on every call - never omit or guess this on the caller's behalf.")]
        string user,
        [Description(
            "Optional free-text explanation of why the results were not useful. Not required even when useful " +
            "is false.")]
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var result = await feedbackService.SubmitAsync(
            projectId,
            question,
            useful,
            similarities,
            reason,
            user,
            cancellationToken);

        return result.Map(
            onSuccess: ToFeedbackToolResult,
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
        result.Similarity,
        result.RerankScore);

    private static CodeQueryFeedbackToolResult ToFeedbackToolResult(FeedbackResult result) => new(
        result.Id,
        result.ProjectId,
        result.Question,
        result.Useful,
        result.Similarities,
        result.Reason,
        result.User,
        result.CreatedAt);
}
