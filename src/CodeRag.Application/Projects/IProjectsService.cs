using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Projects;

public interface IProjectsService
{
    /// <summary>
    /// Lists projects, optionally filtered by a partial, case-insensitive match on their name.
    /// </summary>
    Task<Result<IEnumerable<Project>>> ListAsync(string? nameFilter, CancellationToken cancellationToken = default);
}
