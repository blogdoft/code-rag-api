using CodeRag.Reranking.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace CodeRag.Reranking.Cohere;

public static class ServiceCollectionExtensions
{
    internal const string HttpClientName = "CohereReranker";

    /// <summary>
    /// Registers the Cohere reranking provider factory. This only makes the "Cohere" provider
    /// available for selection; it is only instantiated if <c>Reranking:Provider</c> is set to it.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    public static IServiceCollection AddCohereRerankerProvider(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<IRerankerProviderFactory, CohereRerankerProviderFactory>();
        return services;
    }
}
