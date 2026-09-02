using CodeRag.Reranking.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CodeRag.Reranking.Cohere.Tests;

public sealed class CohereRerankerProviderFactoryTests
{
    [Fact]
    public void Should_ThrowInvalidOperationException_When_ApiKeyIsNotConfigured()
    {
        var services = new ServiceCollection();
        services.AddCohereRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new CohereRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new RerankingOptions { Provider = "Cohere", Model = "rerank-english-v3.0" };

        Should.Throw<InvalidOperationException>(() => factory.Create(options));
    }

    [Fact]
    public void Should_ReturnCohereProviderName()
    {
        var services = new ServiceCollection();
        services.AddCohereRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new CohereRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());

        factory.ProviderName.ShouldBe("Cohere");
    }

    [Fact]
    public void Should_CreateReranker_When_ApiKeyIsConfigured()
    {
        var services = new ServiceCollection();
        services.AddCohereRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new CohereRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new RerankingOptions { Provider = "Cohere", Model = "rerank-english-v3.0", ApiKey = "secret", CandidatePoolSize = 25 };

        var reranker = factory.Create(options);

        reranker.Provider.ShouldBe("Cohere");
        reranker.CandidatePoolSize.ShouldBe(25);
    }
}
