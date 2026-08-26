using CodeRag.Embeddings.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeRag.Embeddings.OpenAI;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "OpenAI";

    /// <summary>
    /// Registers the OpenAI embedding provider factory. This only makes the "OpenAI" provider
    /// available for selection; it is only instantiated if <c>Embeddings:Provider</c> is set to it.
    /// </summary>
    public static IServiceCollection AddOpenAIEmbeddingProvider(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<IEmbeddingProviderFactory, OpenAIEmbeddingProviderFactory>();
        return services;
    }
}
