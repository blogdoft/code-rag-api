namespace CodeRag.Application.CodeQueries;

public interface ICodeDocumentsRepository
{
    /// <summary>
    /// Finds the code documents belonging to <paramref name="projectId"/>, indexed with the
    /// given embedding model, that are most similar to <paramref name="queryEmbedding"/> by
    /// cosine distance, ordered by descending similarity.
    /// </summary>
    Task<IReadOnlyList<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        CancellationToken cancellationToken = default);
}
