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

    /// <summary>Default window size (in days) applied by GetStatsAsync when start/end date are omitted or only one is given.</summary>
    public const int DefaultWindowDays = 30;

    /// <summary>Maximum allowed window size (in days, ~12 months) for GetStatsAsync.</summary>
    public const int MaxWindowDays = 366;

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

    public async Task<Result<FeedbackStatsResult>> GetStatsAsync(
        DateTime? startDate,
        DateTime? endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        // Both given: start_date > end_date is only meaningful to reject when the caller
        // actually supplied both ends themselves - the default-window derivation below can never
        // produce an inverted range on its own.
        if (startDate is not null && endDate is not null && startDate > endDate)
        {
            return FeedbackFailures.InvalidDateRange;
        }

        var effectiveEnd = endDate ?? startDate?.AddDays(DefaultWindowDays) ?? DateTime.UtcNow;
        var effectiveStart = startDate ?? effectiveEnd.AddDays(-DefaultWindowDays);

        if (effectiveEnd - effectiveStart > TimeSpan.FromDays(MaxWindowDays))
        {
            return FeedbackFailures.WindowTooLarge;
        }

        if (projectId is not null)
        {
            var project = await projectsRepository.GetByIdAsync(projectId.Value, cancellationToken);
            if (project is null)
            {
                return FeedbackFailures.ProjectNotFound(projectId.Value);
            }
        }

        var weeks = await feedbackRepository.GetStatsAsync(effectiveStart, effectiveEnd, projectId, cancellationToken);

        return Result<FeedbackStatsResult>.FromSuccess(new FeedbackStatsResult(effectiveStart, effectiveEnd, weeks));
    }
}
