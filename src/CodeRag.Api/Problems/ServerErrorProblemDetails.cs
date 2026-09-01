using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Problems;

/// <summary>
/// Problem Details payload for 500 responses. Exposing the raised exception's details is a
/// deliberate trade-off for ease of debugging in this API, per the OpenAPI contract, and should
/// be disabled or redacted in a hardened deployment.
/// </summary>
public sealed class ServerErrorProblemDetails : ProblemDetails
{
    /// <summary>Details of the unhandled exception that caused the request to fail.</summary>
    public ExceptionDetails Exception { get; init; } = null!;

    /// <summary>Details of the unhandled exception that caused the request to fail.</summary>
    /// <param name="exceptionType">Fully-qualified type name of the exception that was raised.</param>
    /// <param name="message">The exception's message.</param>
    /// <param name="stackTrace">The exception's captured stack trace.</param>
    public sealed record ExceptionDetails(string exceptionType, string message, string? stackTrace);
}
