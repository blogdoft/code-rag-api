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
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    Task<IEnumerable<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        CancellationToken cancellationToken = default);
}
