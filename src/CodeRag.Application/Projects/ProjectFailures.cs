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

    public static Failure NameFilterTooLong(int maxLength) => new(
        "400-name-too-long",
        $"The 'name' query parameter must be at most {maxLength} characters long.");
}
