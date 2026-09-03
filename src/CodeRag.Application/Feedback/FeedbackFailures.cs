using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.Feedback;

/// <summary>
/// Domain failures for the Feedback feature. <see cref="Failure.Code"/> is prefixed with the
/// HTTP status the API layer should map it to, so the API stays a thin, generic translator.
/// </summary>
public static class FeedbackFailures
{
    public static Failure QuestionRequired => new(
        "400-question-required",
        "The 'question' field is required and must not be empty.");

    public static Failure UsefulRequired => new(
        "400-useful-required",
        "The 'useful' field is required.");

    public static Failure SimilaritiesRequired => new(
        "400-similarities-required",
        "The 'similarities' field is required. Send an empty array when the original query returned no results.");

    public static Failure UserRequired => new(
        "400-user-required",
        "The 'user' field is required and must not be empty. For MCP callers, this must be the calling agent/tool's own name.");

    public static Failure InvalidDateRange => new(
        "400-invalid-date-range",
        "The 'start_date' must not be after 'end_date'.");

    public static Failure WindowTooLarge => new(
        "400-window-too-large",
        "The requested time window must not exceed 366 days (12 months).");

    public static Failure QuestionTooLong(int maxLength) => new(
        "400-question-too-long",
        $"The 'question' field must be at most {maxLength} characters long.");

    public static Failure TooManySimilarities(int maxCount) => new(
        "400-too-many-similarities",
        $"The 'similarities' field must contain at most {maxCount} values.");

    public static Failure UserTooLong(int maxLength) => new(
        "400-user-too-long",
        $"The 'user' field must be at most {maxLength} characters long.");

    public static Failure ReasonTooLong(int maxLength) => new(
        "400-reason-too-long",
        $"The 'reason' field must be at most {maxLength} characters long.");

    public static Failure ProjectNotFound(long projectId) => new(
        "404-project-not-found",
        $"No project exists with id {projectId}.");
}
