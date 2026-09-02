namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// Scores/reorders vector-search candidates against a query using more expensive, query-aware
/// logic than raw cosine similarity. Implementations wrap a specific provider (Ollama, Cohere,
/// ...) or, when reranking is disabled, a no-op pass-through; callers depend only on this
/// abstraction and always invoke it unconditionally.
/// </summary>
public interface IReranker
{
    /// <summary>Name of the provider backing this reranker (e.g. "None", "Ollama", "Cohere").</summary>
    string Provider { get; }

    /// <summary>
    /// Number of top vector-search candidates callers should fetch and pass to
    /// <see cref="RerankAsync"/> before truncating to the caller's requested limit. Zero when
    /// reranking is disabled - callers should not request extra candidates in that case.
    /// </summary>
    int CandidatePoolSize { get; }

    /// <summary>
    /// Scores/reorders <paramref name="candidates"/> against <paramref name="query"/>. Returns
    /// every input candidate exactly once, in the reranker's preferred order. A null
    /// <see cref="RerankedCandidate.Score"/> means the candidate was not scored (e.g. reranking
    /// is disabled).
    /// </summary>
    /// <param name="query">Natural language question the candidates are being ranked against.</param>
    /// <param name="candidates">Candidates to score/reorder, as returned by the vector search.</param>
    /// <param name="cancellationToken">Token used to cancel the reranking.</param>
    /// <exception cref="RerankingException">
    /// The provider failed to score the candidates (unreachable service, malformed response, etc.).
    /// </exception>
    Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        CancellationToken cancellationToken = default);
}
