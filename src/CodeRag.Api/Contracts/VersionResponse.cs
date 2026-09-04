namespace CodeRag.Api.Contracts;

/// <summary>The running API's own build version. Serializes as snake_case.</summary>
/// <param name="Version">
/// Semantic version computed by GitVersion from the nearest git tag at publish time (e.g.
/// "1.4.2" on a tagged release, "1.4.3-5" five commits past the last tag on main, or
/// "0.0.0-dev" for a local build that wasn't given an explicit version).
/// </param>
#pragma warning disable SA1313 // positional record parameters are also public properties - PascalCase is correct
public sealed record VersionResponse(string Version);
#pragma warning restore SA1313
