namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// Pass-through reranker used when reranking is disabled (<see cref="RerankingOptions.Provider"/>
/// empty or "None"). Returns every candidate unchanged, in its original order, unscored - this is
/// what lets <c>CodeQueryService</c> call <see cref="IReranker"/> unconditionally with no
/// branching on whether reranking is actually configured.
/// </summary>
internal sealed class NoOpReranker : IReranker
{
    public string Provider => "None";

    public int CandidatePoolSize => 0;

    public Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RerankedCandidate>>(
            candidates.Select(candidate => new RerankedCandidate(candidate.Id, null)).ToList());
}
