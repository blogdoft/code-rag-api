using CodeRag.Embeddings.Abstraction;

namespace CodeRag.Embeddings.Local;

public sealed class LocalEmbeddingProviderFactory : IEmbeddingProviderFactory
{
    public string ProviderName => "Local";

    public IEmbeddingGenerator Create(EmbeddingOptions options) => new LocalEmbeddingGenerator(options);
}
