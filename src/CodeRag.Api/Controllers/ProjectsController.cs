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
    [HttpGet]
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

    private static ProjectResponse ToResponse(Project project) => new(project.Id, project.Name, project.CreatedAt);
}
