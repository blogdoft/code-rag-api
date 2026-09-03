namespace CodeRag.Application.Feedback;

/// <summary>A single raw feedback record, joined with its project's name, for CSV export.</summary>
// SA1313 wants these lower-case, but positional record parameters are also the record's public
// properties - the standard .NET convention (and every consumer's expectation) is PascalCase.
#pragma warning disable SA1313
public sealed record FeedbackExportRow(
    long Id,
    long ProjectId,
    string ProjectName,
    string Question,
    bool Useful,
    IReadOnlyList<double> Similarities,
    string? Reason,
    string Username,
    DateTime CreatedAt);
#pragma warning restore SA1313
