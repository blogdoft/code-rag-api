using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.Projects;

namespace CodeRag.Application.Feedback;

public sealed class FeedbackService(
    IFeedbackRepository feedbackRepository,
    IProjectsRepository projectsRepository) : IFeedbackService
{
    /// <summary>Generous cap on the original natural-language question, mirroring CodeQueryService.MaxQuestionLength.</summary>
    public const int MaxQuestionLength = 1000;

    /// <summary>Generous cap on the caller identity string.</summary>
    public const int MaxUserLength = 200;

    /// <summary>Generous cap on the optional free-text reason.</summary>
    public const int MaxReasonLength = 1000;

    /// <summary>Matches CodeQueryService.MaxResultLimit - a feedback submission can't reference more results than a single query could ever return.</summary>
    public const int MaxSimilaritiesCount = 50;

    public async Task<Result<FeedbackResult>> SubmitAsync(
        long projectId,
        string? question,
        bool? useful,
        IReadOnlyList<double>? similarities,
        string? reason,
        string? user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return FeedbackFailures.QuestionRequired;
        }

        if (question.Length > MaxQuestionLength)
        {
            return FeedbackFailures.QuestionTooLong(MaxQuestionLength);
        }

        if (useful is null)
        {
            return FeedbackFailures.UsefulRequired;
        }

        if (similarities is null)
        {
            return FeedbackFailures.SimilaritiesRequired;
        }

        if (similarities.Count > MaxSimilaritiesCount)
        {
            return FeedbackFailures.TooManySimilarities(MaxSimilaritiesCount);
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            return FeedbackFailures.UserRequired;
        }

        if (user.Length > MaxUserLength)
        {
            return FeedbackFailures.UserTooLong(MaxUserLength);
        }

        if (reason is not null && reason.Length > MaxReasonLength)
        {
            return FeedbackFailures.ReasonTooLong(MaxReasonLength);
        }

        var project = await projectsRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return FeedbackFailures.ProjectNotFound(projectId);
        }

        var feedback = await feedbackRepository.InsertAsync(
            projectId,
            question,
            useful.Value,
            similarities,
            reason,
            user,
            cancellationToken);

        return Result<FeedbackResult>.FromSuccess(feedback);
    }
}
