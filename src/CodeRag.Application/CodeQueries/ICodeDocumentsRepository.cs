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
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    Task<IEnumerable<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        double? minSimilarity,
        CancellationToken cancellationToken = default);
}
