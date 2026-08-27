using BlogDoFT.Libs.ResultPattern;

namespace CodeRag.Application.CodeQueries;

public interface ICodeQueryService
{
    /// <summary>
    /// Embeds <paramref name="question"/> and returns the most semantically similar code
    /// documents indexed for <paramref name="projectId"/>.
    /// </summary>
    /// <param name="projectId">Id of the project to search within.</param>
    /// <param name="question">Natural language description of the code being looked for.</param>
    /// <param name="limit">
    /// Maximum number of results to return. Null falls back to <see cref="CodeQueryService.ResultLimit"/>.
    /// </param>
    /// <param name="minSimilarity">
    /// When set, excludes results whose cosine similarity falls below this value. Null returns
    /// the unfiltered top results.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        int? limit = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default);
}
