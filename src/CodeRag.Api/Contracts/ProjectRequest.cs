using System.Text.Json.Serialization;

namespace CodeRag.Api.Contracts;

/// <summary>
/// Request body for creating (<c>POST /projects</c>) or replacing (<c>PUT /projects/{projectId}</c>)
/// a project. For <c>PUT</c>, this is a full replace: omitting <c>git_url</c>/<c>git_raw_url</c>
/// clears them (sets them to null) rather than leaving the project's current values untouched.
/// </summary>
/// <param name="Name">
/// The project's name. Must be unique across all projects (case-sensitive, exact match) and must
/// not be empty or blank. Attempting to create or rename a project to a name that is already in
/// use results in a 409.
/// </param>
/// <param name="GitUrl">
/// URL of the project's git repository. Optional; omit or send null for a project with no known
/// repository URL. When provided, must not be blank and must be at most 2000 characters long.
/// </param>
/// <param name="GitRawUrl">
/// Base URL for fetching raw file contents from the project's git repository. Optional; omit or
/// send null when not applicable. When provided, must not be blank and must be at most 2000
/// characters long.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
#pragma warning disable SA1313 // positional record parameter is also a public property - PascalCase is correct
public sealed record ProjectRequest(string? Name, string? GitUrl, string? GitRawUrl);
#pragma warning restore SA1313
