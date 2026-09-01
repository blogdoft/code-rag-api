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
    /// <param name="kindOperator">
    /// Comparison operator for the <c>kind</c> filter. Must be set together with
    /// <paramref name="kindValue"/>; null means no <c>kind</c> filter is applied.
    /// </param>
    /// <param name="kindValue">
    /// Filter value compared against <c>kind</c> using <paramref name="kindOperator"/>. For
    /// <c>Contains</c>, <c>*</c> acts as a wildcard; a value with no <c>*</c> is matched exactly
    /// (case-insensitively).
    /// </param>
    /// <param name="namespaceOperator">
    /// Comparison operator for the <c>namespace</c> filter. Must be set together with
    /// <paramref name="namespaceValue"/>; null means no <c>namespace</c> filter is applied.
    /// </param>
    /// <param name="namespaceValue">
    /// Filter value compared against <c>namespace</c> using <paramref name="namespaceOperator"/>.
    /// For <c>Contains</c>/<c>NotContains</c>, <c>*</c> acts as a wildcard; a value with no
    /// <c>*</c> is matched exactly (case-insensitively).
    /// </param>
    /// <param name="typeNameOperator">
    /// Comparison operator for the <c>typeName</c> filter. Must be set together with
    /// <paramref name="typeNameValue"/>; null means no <c>typeName</c> filter is applied.
    /// </param>
    /// <param name="typeNameValue">
    /// Filter value compared against <c>typeName</c> using <paramref name="typeNameOperator"/>.
    /// For <c>Contains</c>/<c>NotContains</c>, <c>*</c> acts as a wildcard; a value with no
    /// <c>*</c> is matched exactly (case-insensitively).
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        int? limit = null,
        double? minSimilarity = null,
        KindFilterOperator? kindOperator = null,
        string? kindValue = null,
        NamespaceFilterOperator? namespaceOperator = null,
        string? namespaceValue = null,
        TypeNameFilterOperator? typeNameOperator = null,
        string? typeNameValue = null,
        CancellationToken cancellationToken = default);
}
