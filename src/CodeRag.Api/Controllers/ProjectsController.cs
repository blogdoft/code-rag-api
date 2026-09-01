using BlogDoFT.Libs.ResultPattern;
using CodeRag.Api.Contracts;
using CodeRag.Api.Problems;
using CodeRag.Application.Projects;
using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Controllers;

/// <summary>
/// CRUD operations for registering, discovering, renaming, and removing indexed projects.
/// </summary>
[ApiController]
[ApiExplorerSettings(GroupName = "Projects")]
[Route("api/v1/projects")]
public sealed class ProjectsController(IProjectsService projectsService) : ControllerBase
{
    private const string GetProjectRouteName = "GetProject";

    /// <summary>List all projects</summary>
    /// <remarks>
    /// Returns all registered projects. Optionally filter the results by project name using a
    /// partial, case-insensitive match against the name field. Use the returned id values as the
    /// projectId path parameter when fetching, renaming, or deleting a specific project, or when
    /// querying code via the /projects/{projectId}/code-queries endpoint.
    ///
    /// When no project matches the supplied filter (or no projects exist at all), the response is
    /// a 200 OK with an empty array - this is not treated as an error.
    /// </remarks>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">
    /// A list of projects matching the given filter, or all projects if no filter was supplied.
    /// Returns an empty array when there are no matches.
    /// </response>
    /// <response code="400">
    /// The name query parameter is invalid (e.g. it exceeds the maximum allowed length, or is
    /// present but empty). This is the only condition under which this endpoint returns 400.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
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

    /// <summary>Get a project by id</summary>
    /// <remarks>Returns a single project by its id.</remarks>
    /// <param name="projectId">
    /// Identifier of the project, corresponding to the id field returned by GET /projects or
    /// POST /projects. Must be a positive 64-bit integer; any other format (e.g. a GUID or
    /// non-numeric string) results in a 400 response.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">The project matching projectId.</response>
    /// <response code="400">
    /// The projectId path parameter is not a valid positive integer. This is the only condition
    /// under which this endpoint returns 400.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
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

    /// <summary>Create a project</summary>
    /// <remarks>
    /// Registers a new project so its source code can later be indexed and searched. The
    /// project's name must be unique across all projects (case-sensitive, exact match); creating
    /// a project with a name that already exists returns a 409. git_url and git_raw_url are
    /// optional and, when provided, are validated but not otherwise checked for reachability.
    ///
    /// On success, the response includes a Location header pointing to the new project's
    /// GET /projects/{projectId} URL, per the standard HTTP convention for 201 Created responses.
    /// </remarks>
    /// <param name="request">The new project's name and, optionally, its git repository details.</param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="201">The project was created.</response>
    /// <response code="400">
    /// The request body is missing or malformed, the name field is missing/empty/blank, name
    /// exceeds the maximum allowed length, or git_url/git_raw_url was provided but is blank or
    /// exceeds the maximum allowed length. This is the only client-error condition other than 409
    /// under which this endpoint fails.
    /// </response>
    /// <response code="409">
    /// A project with the given name already exists. This is the only condition under which this
    /// endpoint returns 409.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
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

    /// <summary>Rename a project</summary>
    /// <remarks>
    /// Replaces the mutable fields of an existing project: name, git_url, and git_raw_url. This is
    /// a full replace, not a partial patch - fields omitted from the request body are cleared (set
    /// to null), so a client that only wants to rename a project must resend its current
    /// git_url/git_raw_url alongside the new name (e.g. from a prior GET /projects/{projectId}).
    /// name is subject to the same uniqueness constraint as POST /projects (renaming a project to
    /// its own current name is allowed and is a no-op). id and created_at are immutable and cannot
    /// be changed.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, corresponding to the id field returned by GET /projects or
    /// POST /projects. Must be a positive 64-bit integer; any other format (e.g. a GUID or
    /// non-numeric string) results in a 400 response.
    /// </param>
    /// <param name="request">The project's new name and git repository details.</param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="200">The project was updated.</response>
    /// <response code="400">
    /// Either the projectId path parameter is not a valid positive integer, or the request body is
    /// missing or malformed, or the name field is missing/empty/blank, or name exceeds the maximum
    /// allowed length, or git_url/git_raw_url was provided but is blank or exceeds the maximum
    /// allowed length. This is the only client-error condition other than 404/409 under which this
    /// endpoint fails.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="409">
    /// Another project already has the given name. This is the only condition under which this
    /// endpoint returns 409.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
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

    /// <summary>Delete a project</summary>
    /// <remarks>
    /// Permanently deletes a project. To prevent silently orphaning or destroying indexed code, a
    /// project that still has indexed code documents cannot be deleted - remove/re-index those
    /// first (outside the scope of this API). This is a hard delete; there is no soft-delete or
    /// undo.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, corresponding to the id field returned by GET /projects or
    /// POST /projects. Must be a positive 64-bit integer; any other format (e.g. a GUID or
    /// non-numeric string) results in a 400 response.
    /// </param>
    /// <param name="cancellationToken">Propagates request abort/timeout to the async pipeline.</param>
    /// <response code="204">The project was deleted. The response has no body.</response>
    /// <response code="400">
    /// The projectId path parameter is not a valid positive integer. This is the only
    /// client-error condition other than 404/409 under which this endpoint fails.
    /// </response>
    /// <response code="404">
    /// No project exists with the given projectId. This is the only condition under which this
    /// endpoint returns 404; the response has no body.
    /// </response>
    /// <response code="409">
    /// The project has one or more indexed code documents and cannot be deleted. This is the only
    /// condition under which this endpoint returns 409.
    /// </response>
    /// <response code="500">
    /// An unhandled exception occurred while processing the request. This is the only condition
    /// under which this endpoint returns 500.
    /// </response>
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
