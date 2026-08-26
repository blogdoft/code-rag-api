namespace CodeRag.Application.Projects;

public interface IProjectsRepository
{
    /// <summary>
    /// Returns every project whose name contains <paramref name="nameFilter"/> (case-insensitive),
    /// or every project when <paramref name="nameFilter"/> is null.
    /// </summary>
    Task<IReadOnlyList<Project>> SearchAsync(string? nameFilter, CancellationToken cancellationToken = default);

    /// <summary>Whether a project with the given id exists.</summary>
    Task<bool> ExistsAsync(long projectId, CancellationToken cancellationToken = default);
}
