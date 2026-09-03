using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Projects;

/// <summary>
/// Domain failures for the Projects feature. <see cref="Failure.Code"/> is prefixed with the
/// HTTP status the API layer should map it to (e.g. "400-...", "404-..."), so the API stays a
/// thin, generic translator instead of hard-coding per-failure status logic.
/// </summary>
public static class ProjectFailures
{
    public static Failure NameFilterEmpty => new(
        "400-name-empty",
        "The 'name' query parameter must not be empty.");

    public static Failure NameRequired => new(
        "400-name-required",
        "The 'name' field is required and must not be empty.");

    public static Failure GitUrlEmpty => new(
        "400-git-url-empty",
        "The 'git_url' field must not be empty when provided.");

    public static Failure GitRawUrlEmpty => new(
        "400-git-raw-url-empty",
        "The 'git_raw_url' field must not be empty when provided.");

    public static Failure NameFilterTooLong(int maxLength) => new(
        "400-name-too-long",
        $"The 'name' query parameter must be at most {maxLength} characters long.");

    public static Failure NameFieldTooLong(int maxLength) => new(
        "400-name-field-too-long",
        $"The 'name' field must be at most {maxLength} characters long.");

    public static Failure GitUrlTooLong(int maxLength) => new(
        "400-git-url-too-long",
        $"The 'git_url' field must be at most {maxLength} characters long.");

    public static Failure GitRawUrlTooLong(int maxLength) => new(
        "400-git-raw-url-too-long",
        $"The 'git_raw_url' field must be at most {maxLength} characters long.");

    public static Failure NotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");

    public static Failure NameAlreadyExists(string name) => new(
        "409-name-already-exists",
        $"A project named '{name}' already exists.");

    public static Failure HasIndexedCodeDocuments(long projectId) => new(
        "409-has-indexed-code-documents",
        $"Project {projectId} has indexed code documents and cannot be deleted. Remove its indexed code documents first.");

    public static Failure HasFeedback(long projectId) => new(
        "409-has-feedback",
        $"Project {projectId} has feedback records and cannot be deleted. Remove its feedback records first.");
}
