using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.CodeQueries;

/// <summary>
/// Domain failures for the Code Query feature. <see cref="Failure.Code"/> is prefixed with the
/// HTTP status the API layer should map it to, so the API stays a thin, generic translator.
/// </summary>
public static class CodeQueryFailures
{
    public static Failure QuestionRequired => new(
        "400-question-required",
        "The 'question' field is required and must not be empty.");

    public static Failure ProjectNotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");
}
