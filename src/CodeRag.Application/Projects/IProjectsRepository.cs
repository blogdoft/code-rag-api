namespace CodeRag.Application.Projects;

public interface IProjectsRepository
{
    /// <summary>
    /// Returns every project whose name contains <paramref name="nameFilter"/> (case-insensitive),
    /// or every project when <paramref name="nameFilter"/> is null.
    /// </summary>
    /// <param name="nameFilter">Partial, case-insensitive name filter, or null to match every project.</param>
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    Task<IEnumerable<Project>> SearchAsync(string? nameFilter, CancellationToken cancellationToken = default);

    /// <summary>Whether a project with the given id exists.</summary>
    /// <param name="projectId">Id of the project to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<bool> ExistsAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>Returns the project with the given id, or null if none exists.</summary>
    /// <param name="projectId">Id of the project to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>Whether a project named <paramref name="name"/> already exists (exact, case-sensitive match).</summary>
    /// <param name="name">Name to look up.</param>
    /// <param name="excludingProjectId">
    /// When set, excludes the project with this id from the check - used when renaming a project
    /// so it doesn't collide with its own current name.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<bool> NameExistsAsync(string name, long? excludingProjectId = null, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new project and returns the persisted row, including its generated id and created_at.</summary>
    /// <param name="name">Name of the project to create.</param>
    /// <param name="gitUrl">URL of the project's git repository, or null.</param>
    /// <param name="gitRawUrl">Base URL for fetching raw file contents from the project's git repository, or null.</param>
    /// <param name="cancellationToken">Token used to cancel the insert.</param>
    Task<Project> InsertAsync(
        string name,
        string? gitUrl,
        string? gitRawUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the mutable fields of the project with the given id and returns the updated row,
    /// or null if no project with that id exists.
    /// </summary>
    /// <param name="projectId">Id of the project to update.</param>
    /// <param name="name">New name for the project.</param>
    /// <param name="gitUrl">New URL of the project's git repository, or null.</param>
    /// <param name="gitRawUrl">New base URL for fetching raw file contents from the project's git repository, or null.</param>
    /// <param name="cancellationToken">Token used to cancel the update.</param>
    Task<Project?> UpdateAsync(
        long projectId,
        string name,
        string? gitUrl,
        string? gitRawUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the project with the given id.</summary>
    /// <param name="projectId">Id of the project to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the delete.</param>
    /// <returns>Whether a row was actually deleted (false if no project with that id existed).</returns>
    Task<bool> DeleteAsync(long projectId, CancellationToken cancellationToken = default);
}
