using CodeRag.Embeddings.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeRag.Embeddings.Local;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Local embedding provider factory. This only makes the "Local" provider
    /// available for selection; it is only instantiated if <c>Embeddings:Provider</c> is set to it.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingProvider(this IServiceCollection services)
    {
        services.AddSingleton<IEmbeddingProviderFactory, LocalEmbeddingProviderFactory>();
        return services;
    }
}
