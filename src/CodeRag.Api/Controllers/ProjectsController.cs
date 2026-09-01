using BlogDoFT.Libs.ResultPattern;
using CodeRag.Api.Contracts;
using CodeRag.Api.Problems;
using CodeRag.Application.Projects;
using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    private const string GetProjectRouteName = "GetProject";

    [HttpGet]
    [ProducesResponseType<IEnumerable<ProjectResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        // Read the raw query value instead of a bound [FromQuery] parameter: MVC's default
        // model binding treats an empty string as null (ConvertEmptyStringToNull), which would
        // make "?name=" indistinguishable from omitting the parameter entirely - the OpenAPI
        // contract requires the former to be a 400.
#pragma warning disable S6932
        var name = Request.Query.TryGetValue("name", out var values) ? values.ToString() : null;
#pragma warning restore S6932

        var result = await projectsService.ListAsync(name, cancellationToken);

        return result.Map(
            onSuccess: projects => (IActionResult)Ok(projects.Select(ToResponse)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    [HttpGet("{projectId}", Name = GetProjectRouteName)]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> GetAsync(string projectId, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await projectsService.GetAsync(id, cancellationToken);

        return result.Map(
            onSuccess: project => (IActionResult)Ok(ToResponse(project)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    [HttpPost]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> CreateAsync([FromBody] ProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await projectsService.CreateAsync(request.Name, request.GitUrl, request.GitRawUrl, cancellationToken);

        return result.Map(
            onSuccess: project => (IActionResult)CreatedAtRoute(
                GetProjectRouteName,
                new { projectId = project.Id },
                ToResponse(project)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    [HttpPut("{projectId}")]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> UpdateAsync(
        string projectId,
        [FromBody] ProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await projectsService.UpdateAsync(id, request.Name, request.GitUrl, request.GitRawUrl, cancellationToken);

        return result.Map(
            onSuccess: project => (IActionResult)Ok(ToResponse(project)),
            onFailure: failure => failure.ToActionResult(HttpContext));
    }

    [HttpDelete("{projectId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ServerErrorProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> DeleteAsync(string projectId, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParsePositive(projectId, "projectId", HttpContext.Request.Path, out var id, out var problem))
        {
            return problem!;
        }

        var result = await projectsService.DeleteAsync(id, cancellationToken);

        return result.IsFailure ? result.Failure.ToActionResult(HttpContext) : NoContent();
    }

    private static ProjectResponse ToResponse(Project project) =>
        new(project.Id, project.Name, project.GitUrl, project.GitRawUrl, project.CreatedAt);
}
