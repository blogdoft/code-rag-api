using Microsoft.Extensions.Options;

namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// Picks the <see cref="IReranker"/> matching the configured provider out of every
/// <see cref="IRerankerProviderFactory"/> registered in the container.
/// </summary>
public sealed class RerankerResolver
{
    private readonly IReadOnlyDictionary<string, IRerankerProviderFactory> _factories;
    private readonly RerankingOptions _options;

    public RerankerResolver(
        IEnumerable<IRerankerProviderFactory> factories,
        IOptions<RerankingOptions> options)
    {
        _factories = factories.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    /// <summary>
    /// Resolves the configured reranker.
    /// </summary>
    /// <remarks>
    /// Unlike <c>EmbeddingGeneratorResolver</c>, reranking is optional: an empty or "None"
    /// <see cref="RerankingOptions.Provider"/> is not a misconfiguration - it is the documented
    /// way to disable reranking, so it resolves to a <see cref="NoOpReranker"/> instead of
    /// throwing. A non-empty but unknown provider name is still a startup-time operator error
    /// (a typo in config) and still fails fast, exactly like embeddings.
    /// </remarks>
    public IReranker Resolve()
    {
        if (string.IsNullOrWhiteSpace(_options.Provider) ||
            _options.Provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return new NoOpReranker();
        }

        if (!_factories.TryGetValue(_options.Provider, out var factory))
        {
            throw new InvalidOperationException(
                $"Unknown reranking provider '{_options.Provider}'. Available providers: None, " +
                string.Join(", ", _factories.Keys));
        }

        return factory.Create(_options);
    }
}
