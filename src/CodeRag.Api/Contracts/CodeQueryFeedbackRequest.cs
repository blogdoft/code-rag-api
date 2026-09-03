using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>Feedback on a prior <c>POST .../code-queries</c> call, scoped to a single project.</summary>
/// <param name="Question">
/// The natural language question that was originally sent to <c>POST .../code-queries</c>. Must
/// not be empty or blank.
/// </param>
/// <param name="Useful">Whether the results returned for <c>question</c> were useful to the caller.</param>
/// <param name="Similarities">
/// The exact <c>similarity</c> values (not <c>rerankScore</c>) returned by the original
/// code-queries call, in the order they were received. May be an empty array when the query
/// returned zero results.
/// </param>
/// <param name="Reason">
/// Optional free-text explanation of why the results were not useful. Not required even when
/// <c>useful</c> is false.
/// </param>
/// <param name="User">
/// Identity of the caller submitting this feedback. For a human caller, their own identifier
/// (e.g. a username). For an MCP caller, the name of the calling agent/tool itself (e.g.
/// "claude code", "codex", "crewai", "hermes", "opencode") - never omitted or guessed.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record CodeQueryFeedbackRequest(
    string? Question,
    bool? Useful,
    IReadOnlyList<double>? Similarities,
    string? Reason,
    string? User);
#pragma warning restore SA1313
