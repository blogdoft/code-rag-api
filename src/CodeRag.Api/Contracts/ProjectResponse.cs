namespace CodeRag.Api.Contracts;

/// <summary>
/// A project whose source code has been analyzed and indexed for semantic search. Serializes as
/// snake_case.
/// </summary>
/// <param name="Id">
/// Auto-generated, unique identifier of the project. Used as the <c>projectId</c> path parameter
/// when querying code for this project.
/// </param>
/// <param name="Name">
/// Unique, human-readable name of the project. Used as the searchable field for
/// <c>GET /projects?name=</c>, and as the field set by <c>POST /projects</c> and
/// <c>PUT /projects/{projectId}</c>.
/// </param>
/// <param name="GitUrl">
/// URL of the project's git repository, e.g. its clone/remote URL. Optional; null when not set.
/// Not validated for reachability - only for being non-blank and within the maximum allowed
/// length when provided.
/// </param>
/// <param name="GitRawUrl">
/// Base URL for fetching raw file contents from the project's git repository, e.g. a "raw"
/// content host URL that a client can append a matched code document's source file path to in
/// order to link directly to the matched source. Optional; null when not set. Not validated for
/// reachability - only for being non-blank and within the maximum allowed length when provided.
/// </param>
/// <param name="CreatedAt">
/// Timestamp (UTC) at which the project record was created. Defaults to the insertion time on
/// the database if not otherwise specified.
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record ProjectResponse(long Id, string Name, string? GitUrl, string? GitRawUrl, DateTime CreatedAt);
#pragma warning restore SA1313
