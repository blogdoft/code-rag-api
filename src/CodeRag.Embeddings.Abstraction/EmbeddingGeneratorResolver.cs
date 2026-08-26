using Microsoft.Extensions.Options;

namespace CodeRag.Embeddings.Abstraction;

/// <summary>
/// Picks the <see cref="IEmbeddingGenerator"/> matching the configured provider out of every
/// <see cref="IEmbeddingProviderFactory"/> registered in the container.
/// </summary>
public sealed class EmbeddingGeneratorResolver
{
    private readonly IReadOnlyDictionary<string, IEmbeddingProviderFactory> _factories;
    private readonly EmbeddingOptions _options;

    public EmbeddingGeneratorResolver(
        IEnumerable<IEmbeddingProviderFactory> factories,
        IOptions<EmbeddingOptions> options)
    {
        _factories = factories.ToDictionary(f => f.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
    }

    /// <summary>
    /// Resolves the configured embedding generator.
    /// </summary>
    /// <remarks>
    /// This runs once, at composition-root time. A misconfigured provider name is an
    /// operator error, not a request-time domain failure, so it fails fast here rather
    /// than being modeled as a <c>Result</c>.
    /// </remarks>
    public IEmbeddingGenerator Resolve()
    {
        if (string.IsNullOrWhiteSpace(_options.Provider))
        {
            throw new InvalidOperationException(
                $"No embedding provider configured. Set '{EmbeddingOptions.SectionName}:Provider' to one of: " +
                string.Join(", ", _factories.Keys));
        }

        if (!_factories.TryGetValue(_options.Provider, out var factory))
        {
            throw new InvalidOperationException(
                $"Unknown embedding provider '{_options.Provider}'. Available providers: " +
                string.Join(", ", _factories.Keys));
        }

        return factory.Create(_options);
    }
}
