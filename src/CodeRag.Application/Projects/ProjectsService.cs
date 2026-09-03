using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.CodeQueries;
using CodeRag.Application.Feedback;

namespace CodeRag.Application.Projects;

public sealed class ProjectsService(
    IProjectsRepository repository,
    ICodeDocumentsRepository codeDocumentsRepository,
    IFeedbackRepository feedbackRepository) : IProjectsService
{
    /// <summary>Matches the <c>maxLength</c> constraint on the <c>name</c> query parameter in the OpenAPI contract.</summary>
    public const int MaxNameFilterLength = 200;

    /// <summary>Matches the <c>maxLength</c> constraint on the <c>name</c> field of the request body in the OpenAPI contract.</summary>
    public const int MaxNameLength = 200;

    /// <summary>
    /// Matches the <c>maxLength</c> constraint on the <c>git_url</c>/<c>git_raw_url</c> fields of
    /// the request body in the OpenAPI contract.
    /// </summary>
    public const int MaxGitUrlLength = 2000;

    public async Task<Result<IEnumerable<Project>>> ListAsync(
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

        var projects = await repository.SearchAsync(nameFilter, cancellationToken);
        return Result<IEnumerable<Project>>.FromSuccess(projects);
    }

    public async Task<Result<Project>> GetAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var project = await repository.GetByIdAsync(projectId, cancellationToken);
        return project is not null ? Result<Project>.FromSuccess(project) : ProjectFailures.NotFound(projectId);
    }

    public async Task<Result<Project>> CreateAsync(
        string? name,
        string? gitUrl = null,
        string? gitRawUrl = null,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateName(name) ?? ValidateGitUrl(gitUrl) ?? ValidateGitRawUrl(gitRawUrl);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        // Checked here rather than relying solely on the database's unique constraint, so a
        // duplicate name reports as a domain-modeled 409 instead of a raw constraint-violation
        // 500. This leaves a small, accepted race window between the check and the insert below.
        if (await repository.NameExistsAsync(name!, cancellationToken: cancellationToken))
        {
            return ProjectFailures.NameAlreadyExists(name!);
        }

        var project = await repository.InsertAsync(name!, gitUrl, gitRawUrl, cancellationToken);
        return Result<Project>.FromSuccess(project);
    }

    public async Task<Result<Project>> UpdateAsync(
        long projectId,
        string? name,
        string? gitUrl = null,
        string? gitRawUrl = null,
        CancellationToken cancellationToken = default)
    {
        var validationFailure = ValidateName(name) ?? ValidateGitUrl(gitUrl) ?? ValidateGitRawUrl(gitRawUrl);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        // Same accepted check-then-act race as CreateAsync.
        if (await repository.NameExistsAsync(name!, projectId, cancellationToken))
        {
            return ProjectFailures.NameAlreadyExists(name!);
        }

        var project = await repository.UpdateAsync(projectId, name!, gitUrl, gitRawUrl, cancellationToken);
        return project is not null ? Result<Project>.FromSuccess(project) : ProjectFailures.NotFound(projectId);
    }

    public async Task<Result> DeleteAsync(long projectId, CancellationToken cancellationToken = default)
    {
        var hasCodeDocuments = await codeDocumentsRepository.ExistsForProjectAsync(projectId, cancellationToken);
        if (hasCodeDocuments)
        {
            return ProjectFailures.HasIndexedCodeDocuments(projectId);
        }

        var hasFeedback = await feedbackRepository.ExistsForProjectAsync(projectId, cancellationToken);
        if (hasFeedback)
        {
            return ProjectFailures.HasFeedback(projectId);
        }

        // No separate existence check is needed: when projectId doesn't exist, DeleteAsync
        // below simply affects zero rows and reports it via its bool return.
        var deleted = await repository.DeleteAsync(projectId, cancellationToken);
        return deleted ? Result.AsSuccess() : ProjectFailures.NotFound(projectId);
    }

    private static Failure? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ProjectFailures.NameRequired;
        }

        if (name.Length > MaxNameLength)
        {
            return ProjectFailures.NameFieldTooLong(MaxNameLength);
        }

        return null;
    }

    private static Failure? ValidateGitUrl(string? gitUrl) =>
        ValidateOptionalUrl(gitUrl, ProjectFailures.GitUrlEmpty, ProjectFailures.GitUrlTooLong);

    private static Failure? ValidateGitRawUrl(string? gitRawUrl) =>
        ValidateOptionalUrl(gitRawUrl, ProjectFailures.GitRawUrlEmpty, ProjectFailures.GitRawUrlTooLong);

    // git_url/git_raw_url are optional: null means "not set". A non-null value is still
    // validated so a caller can't silently persist a blank or absurdly long string instead.
    private static Failure? ValidateOptionalUrl(string? value, Failure emptyFailure, Func<int, Failure> tooLongFailure)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return emptyFailure;
        }

        if (value.Length > MaxGitUrlLength)
        {
            return tooLongFailure(MaxGitUrlLength);
        }

        return null;
    }
}
