using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Projects;

public sealed class ProjectsService(IProjectsRepository repository) : IProjectsService
{
    /// <summary>Matches the <c>maxLength</c> constraint on the <c>name</c> query parameter in the OpenAPI contract.</summary>
    public const int MaxNameFilterLength = 200;

    public async Task<Result<Project[]>> ListAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default)
    {
        if (nameFilter is not null)
        {
            if (nameFilter.Length == 0)
            {
                return ProjectFailures.NameFilterEmpty;
            }

            if (nameFilter.Length > MaxNameFilterLength)
            {
                return ProjectFailures.NameFilterTooLong(MaxNameFilterLength);
            }
        }

        var projects = await repository.SearchAsync(nameFilter, cancellationToken).ConfigureAwait(false);
        return projects.ToArray();
    }
}
