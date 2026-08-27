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
}
