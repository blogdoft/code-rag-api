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
}
