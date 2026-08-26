namespace CodeRag.Embeddings.Abstraction;

/// <summary>
/// Creates an <see cref="IEmbeddingGenerator"/> for a specific provider. Each provider
/// module (Local, Ollama, OpenAI, ...) registers one factory into DI; the
/// <see cref="EmbeddingGeneratorResolver"/> picks whichever matches the configured
/// <see cref="EmbeddingOptions.Provider"/> value. New providers plug in without any
/// change to the resolver.
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>Provider name this factory handles, matched case-insensitively against configuration.</summary>
    string ProviderName { get; }

    /// <summary>Builds the generator for this provider using the supplied options.</summary>
    IEmbeddingGenerator Create(EmbeddingOptions options);
}
