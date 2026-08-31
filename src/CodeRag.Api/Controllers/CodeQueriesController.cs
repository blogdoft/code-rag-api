using BlogDoFT.Libs.ResultPattern;
using CodeRag.Api.Contracts;
using CodeRag.Api.Problems;
using CodeRag.Application.CodeQueries;
using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId}/code-queries")]
public sealed class CodeQueriesController(ICodeQueryService codeQueryService) : ControllerBase
{
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
        if (!long.TryParse(projectId, out var id) || id < 1)
        {
            return ProblemResults.BadRequest(
                $"The 'projectId' route parameter must be a positive integer; received '{projectId}'.",
                HttpContext.Request.Path);
        }

        var result = await codeQueryService.QueryAsync(id, request.Question, cancellationToken: cancellationToken);

        return result.Map(
            onSuccess: results => (IActionResult)Ok(results.Select(ToResponse)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    private static CodeQueryResultResponse ToResponse(CodeQueryResult result) => new(
        result.Id,
        result.SourceFile,
        result.Kind,
        result.TypeName,
        result.Member,
        result.EmbeddingText,
        result.Similarity);
}
