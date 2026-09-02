using CodeRag.Reranking.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CodeRag.Reranking.Ollama.Tests;

public sealed class OllamaRerankerProviderFactoryTests
{
    [Fact]
    public void Should_ThrowInvalidOperationException_When_BaseUrlIsNotConfigured()
    {
        var services = new ServiceCollection();
        services.AddOllamaRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new RerankingOptions { Provider = "Ollama", Model = "qwen2.5:7b-instruct" };

        Should.Throw<InvalidOperationException>(() => factory.Create(options));
    }

    [Fact]
    public void Should_ReturnOllamaProviderName()
    {
        var services = new ServiceCollection();
        services.AddOllamaRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());

        factory.ProviderName.ShouldBe("Ollama");
    }

    [Fact]
    public void Should_CreateReranker_When_BaseUrlIsConfigured()
    {
        var services = new ServiceCollection();
        services.AddOllamaRerankerProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaRerankerProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new RerankingOptions
        {
            Provider = "Ollama",
            Model = "qwen2.5:7b-instruct",
            BaseUrl = "http://localhost:11434",
            CandidatePoolSize = 25,
        };

        var reranker = factory.Create(options);

        reranker.Provider.ShouldBe("Ollama");
        reranker.CandidatePoolSize.ShouldBe(25);
    }
}
