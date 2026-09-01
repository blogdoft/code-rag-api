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

    public static Failure MinSimilarityOutOfRange => new(
        "400-min-similarity-out-of-range",
        "The 'minSimilarity' field must be between 0.0 and 1.0.");

    public static Failure KindFilterValueRequired => new(
        "400-kind-filter-value-required",
        "The 'kind' filter's 'value' field is required and must not be empty.");

    public static Failure NamespaceFilterValueRequired => new(
        "400-namespace-filter-value-required",
        "The 'namespace' filter's 'value' field is required and must not be empty.");

    public static Failure TypeNameFilterValueRequired => new(
        "400-type-name-filter-value-required",
        "The 'typeName' filter's 'value' field is required and must not be empty.");

    public static Failure QuestionTooLong(int maxLength) => new(
        "400-question-too-long",
        $"The 'question' field must be at most {maxLength} characters long.");

    public static Failure ProjectNotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");

    public static Failure LimitOutOfRange(int min, int max) => new(
        "400-limit-out-of-range",
        $"The 'limit' field must be between {min} and {max}.");

    public static Failure KindFilterValueTooLong(int maxLength) => new(
        "400-kind-filter-value-too-long",
        $"The 'kind' filter's 'value' field must be at most {maxLength} characters long.");

    public static Failure NamespaceFilterValueTooLong(int maxLength) => new(
        "400-namespace-filter-value-too-long",
        $"The 'namespace' filter's 'value' field must be at most {maxLength} characters long.");

    public static Failure TypeNameFilterValueTooLong(int maxLength) => new(
        "400-type-name-filter-value-too-long",
        $"The 'typeName' filter's 'value' field must be at most {maxLength} characters long.");
}
