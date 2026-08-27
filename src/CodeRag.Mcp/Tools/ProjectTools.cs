using System.ComponentModel;
using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.Projects;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CodeRag.Mcp.Tools;

/// <summary>MCP tools for discovering indexed code projects, backed directly by the Application layer.</summary>
[McpServerToolType]
public sealed class ProjectTools(IProjectsService projectsService)
{
    [McpServerTool(Name = "list_projects", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Lists the code projects that have been indexed and are available for semantic code research. " +
        "Optionally filter by a partial, case-insensitive match on the project name. Call this first to " +
        "discover the projectId required by the query_project_code tool.")]
    public async Task<IReadOnlyList<ProjectToolResult>> ListProjects(
        [Description(
            "Partial, case-insensitive filter on the project name (e.g. 'cart' matches 'shopping-cart-service'). " +
            "Omit to list every indexed project.")]
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var result = await projectsService.ListAsync(name, cancellationToken);

        return result.Map<Project[], IReadOnlyList<ProjectToolResult>>(
            onSuccess: projects => projects.Select(ToToolResult).ToArray(),
            onFailure: failure => throw new McpException(failure.Message));
    }

    private static ProjectToolResult ToToolResult(Project project) => new(project.Id, project.Name, project.CreatedAt);
}
