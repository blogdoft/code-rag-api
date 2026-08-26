using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodeRag.Api.Problems;

/// <summary>Builds RFC 7807 "application/problem+json" results with the fields the OpenAPI contract expects.</summary>
public static class ProblemResults
{
    public static IActionResult BadRequest(string detail, PathString instance) =>
        Build(StatusCodes.Status400BadRequest, "Bad Request", detail, instance);

    public static IActionResult Build(int status, string title, string detail, PathString instance)
    {
        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.io/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = instance,
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
