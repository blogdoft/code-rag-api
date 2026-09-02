namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// Creates an <see cref="IReranker"/> for a specific provider. Each provider module (Ollama,
/// Cohere, ...) registers one factory into DI; the <see cref="RerankerResolver"/> picks
/// whichever matches the configured <see cref="RerankingOptions.Provider"/> value. New
/// providers plug in without any change to the resolver.
/// </summary>
public interface IRerankerProviderFactory
{
    /// <summary>Provider name this factory handles, matched case-insensitively against configuration.</summary>
    string ProviderName { get; }

    /// <summary>Builds the reranker for this provider using the supplied options.</summary>
    /// <param name="options">Configuration for the reranker, bound from the "Reranking" section.</param>
    IReranker Create(RerankingOptions options);
}
