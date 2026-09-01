namespace CodeRag.Application.CodeQueries;

public interface ICodeDocumentsRepository
{
    /// <summary>
    /// Finds the code documents belonging to <paramref name="projectId"/>, indexed with the
    /// given embedding model, that are most similar to <paramref name="queryEmbedding"/> by
    /// cosine distance, ordered by descending similarity.
    /// </summary>
    /// <param name="projectId">Id of the project to search within.</param>
    /// <param name="embeddingProvider">Provider that produced <paramref name="queryEmbedding"/> (e.g. "Ollama").</param>
    /// <param name="embeddingModel">Model that produced <paramref name="queryEmbedding"/>.</param>
    /// <param name="embeddingDimensions">Dimensionality of <paramref name="queryEmbedding"/>.</param>
    /// <param name="queryEmbedding">Embedding to compare indexed documents against.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="minSimilarity">
    /// When set, excludes documents whose cosine similarity to <paramref name="queryEmbedding"/>
    /// falls below this value. Null returns the top <paramref name="limit"/> unfiltered.
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
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    Task<IEnumerable<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        double? minSimilarity,
        KindFilterOperator? kindOperator = null,
        string? kindValue = null,
        NamespaceFilterOperator? namespaceOperator = null,
        string? namespaceValue = null,
        TypeNameFilterOperator? typeNameOperator = null,
        string? typeNameValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether any code document is indexed for the given project.</summary>
    /// <param name="projectId">Id of the project to check.</param>
    /// <param name="cancellationToken">Token used to cancel the lookup.</param>
    Task<bool> ExistsForProjectAsync(long projectId, CancellationToken cancellationToken = default);
}
