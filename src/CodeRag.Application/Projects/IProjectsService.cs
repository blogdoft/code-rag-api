using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Projects;

public interface IProjectsService
{
    /// <summary>
    /// Lists projects, optionally filtered by a partial, case-insensitive match on their name.
    /// </summary>
    /// <param name="nameFilter">Partial, case-insensitive name filter, or null to list every project.</param>
    /// <param name="cancellationToken">Token used to cancel the listing.</param>
    Task<Result<IEnumerable<Project>>> ListAsync(string? nameFilter, CancellationToken cancellationToken = default);

    /// <summary>Returns the project with the given id.</summary>
    /// <param name="projectId">Id of the project to fetch.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>Creates a new project.</summary>
    /// <param name="name">Name for the new project. Must be non-empty and unique.</param>
    /// <param name="gitUrl">URL of the project's git repository. Optional.</param>
    /// <param name="gitRawUrl">Base URL for fetching raw file contents from the project's git repository. Optional.</param>
    /// <param name="cancellationToken">Token used to cancel the creation.</param>
    Task<Result<Project>> CreateAsync(
        string? name,
        string? gitUrl = null,
        string? gitRawUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the mutable fields of an existing project.</summary>
    /// <param name="projectId">Id of the project to update.</param>
    /// <param name="name">New name for the project. Must be non-empty and unique.</param>
    /// <param name="gitUrl">New URL of the project's git repository. Optional.</param>
    /// <param name="gitRawUrl">New base URL for fetching raw file contents from the project's git repository. Optional.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    Task<Result<Project>> UpdateAsync(
        long projectId,
        string? name,
        string? gitUrl = null,
        string? gitRawUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a project. Fails if the project has indexed code documents, so that deleting a
    /// project can never silently orphan or destroy indexed code.
    /// </summary>
    /// <param name="projectId">Id of the project to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the deletion.</param>
    Task<Result> DeleteAsync(long projectId, CancellationToken cancellationToken = default);
}
