namespace CodeRag.Application.Feedback;

/// <summary>Feedback effectiveness statistics for a single project within a single week.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record ProjectFeedbackStats(
    long ProjectId,
    string ProjectName,
    long TotalCount,
    long UsefulCount,
    long NotUsefulCount,
    double UsefulPercentage,
    double NotUsefulPercentage);
#pragma warning restore SA1313
