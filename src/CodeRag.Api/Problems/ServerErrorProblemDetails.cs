using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Problems;

/// <summary>
/// Problem Details payload for 500 responses. Exposing the raised exception's details is a
/// deliberate trade-off for ease of debugging in this API, per the OpenAPI contract, and should
/// be disabled or redacted in a hardened deployment.
/// </summary>
public sealed class ServerErrorProblemDetails : ProblemDetails
{
    public ExceptionDetails Exception { get; init; } = null!;

    public sealed record ExceptionDetails(string ExceptionType, string Message, string? StackTrace);
}
