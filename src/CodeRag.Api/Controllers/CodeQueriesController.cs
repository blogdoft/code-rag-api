using BlogDoFT.Libs.ResultPattern;
using CodeRag.Api.Contracts;
using CodeRag.Api.Problems;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;
using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Controllers;

/// <summary>Operations for querying indexed source code using natural language.</summary>
[ApiController]
[ApiExplorerSettings(GroupName = "Code Query")]
[Route("api/v1/projects/{projectId}/code-queries")]
public sealed class CodeQueriesController(ICodeQueryService codeQueryService, IFeedbackService feedbackService) : ControllerBase
{
    /// <summary>Query a project's indexed code using natural language</summary>
    /// <remarks>
    /// Accepts a natural language question about a project's codebase, converts it into a
    /// vector embedding, and returns the code documents from that project whose stored
    /// embeddings are most semantically similar to the question.
    ///
    /// When the project exists but no code document scores highly enough to be considered a
    /// match, the response is a 200 OK with an empty array - this is not treated as an error.
    /// A 404 is returned only when the projectId itself does not correspond to any project.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project to search, corresponding to the <c>id</c> field returned by
    /// <c>GET /projects</c>. Must be a positive 64-bit integer; any other format (e.g. a GUID or
    /// non-numeric string) results in a 400 response.
    /// </param>
    /// <param name="request">
    /// The natural language question to search the project's code with, plus any optional
    /// <c>kind</c>/<c>namespace</c>/<c>typeName</c> filters.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// The code documents most semantically similar to the natural language question, ordered by
    /// descending relevance - by rerank score when reranking is enabled, otherwise by cosine
    /// similarity. Returns an empty array when the project has no code documents, or none are
    /// found to be similar enough to the question.
    /// </response>
    /// <response code="400">
    /// Either the projectId path parameter is not a valid positive integer (e.g. a GUID was
    /// supplied), the request body is missing the question field, has an empty/blank question, an
    /// optional filter's value is empty/blank or too long, or the request is otherwise malformed.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpPost]
    [ProducesResponseType<IEnumerable<CodeQueryResultResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> QueryAsync(
        string projectId,
        [FromBody] CodeQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await codeQueryService.QueryAsync(
            id,
            request.Question,
            kindOperator: request.Kind?.Operator,
            kindValue: request.Kind?.Value,
            namespaceOperator: request.Namespace?.Operator,
            namespaceValue: request.Namespace?.Value,
            typeNameOperator: request.TypeName?.Operator,
            typeNameValue: request.TypeName?.Value,
            cancellationToken: cancellationToken);

        return result.Map(
            onSuccess: results => (IActionResult)Ok(results.Select(ToResponse)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    /// <summary>Submit feedback on a prior code query</summary>
    /// <remarks>
    /// Records whether a previous <c>POST .../code-queries</c> call's results were useful, so
    /// effectiveness can be measured later. Callable by a human (REST) or an AI agent (MCP, via
    /// the <c>submit_code_query_feedback</c> tool). The caller must always identify itself via
    /// the required <c>user</c> field - for MCP callers this is the calling agent/tool's own
    /// name (e.g. "claude code", "codex", "crewai", "hermes", "opencode"), never a guess or a
    /// default.
    ///
    /// There is no corresponding GET endpoint for feedback in this iteration - effectiveness is
    /// computed directly against the database out of band. Accordingly, unlike
    /// <c>POST /projects</c>, the 201 response below has no <c>Location</c> header.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project the original code-queries call was scoped to, corresponding to
    /// the <c>id</c> field returned by <c>GET /projects</c>. Must be a positive 64-bit integer;
    /// any other format (e.g. a GUID or non-numeric string) results in a 400 response.
    /// </param>
    /// <param name="request">
    /// The original question, whether it was useful, the similarity values it returned, and the
    /// identity of the caller submitting the feedback.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="201">
    /// The feedback was recorded. The response body echoes the persisted record, including its
    /// generated id and created_at.
    /// </response>
    /// <response code="400">
    /// Either the projectId path parameter is not a valid positive integer, the request body is
    /// missing or malformed, question/useful/similarities/user is missing or invalid (empty,
    /// blank, exceeds its maximum length/count), or reason exceeds its maximum length.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
    [HttpPost("feedback")]
    [ProducesResponseType<CodeQueryFeedbackResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> SubmitFeedbackAsync(
        string projectId,
        [FromBody] CodeQueryFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await feedbackService.SubmitAsync(
            id,
            request.Question,
            request.Useful,
            request.Similarities,
            request.Reason,
            request.User,
            cancellationToken);

        return result.Map(
            onSuccess: feedback => (IActionResult)StatusCode(StatusCodes.Status201Created, ToFeedbackResponse(feedback)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    private static CodeQueryResultResponse ToResponse(CodeQueryResult result) => new(
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

    private static CodeQueryFeedbackResponse ToFeedbackResponse(FeedbackResult result) => new(
        result.Id,
        result.ProjectId,
        result.Question,
        result.Useful,
        result.Similarities,
        result.Reason,
        result.User,
        result.CreatedAt);
}
