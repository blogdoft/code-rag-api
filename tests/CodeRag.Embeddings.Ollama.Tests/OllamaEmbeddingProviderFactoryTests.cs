using CodeRag.Embeddings.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CodeRag.Embeddings.Ollama.Tests;

public sealed class OllamaEmbeddingProviderFactoryTests
{
    [Fact]
    public void Should_ThrowInvalidOperationException_When_BaseUrlIsNotConfigured()
    {
        var services = new ServiceCollection();
        services.AddOllamaEmbeddingProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaEmbeddingProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new EmbeddingOptions { Provider = "Ollama", Model = "bge-m3", Dimensions = 3 };

        Should.Throw<InvalidOperationException>(() => factory.Create(options));
    }

    [Fact]
    public void Should_ReturnOllamaProviderName()
    {
        var services = new ServiceCollection();
        services.AddOllamaEmbeddingProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaEmbeddingProviderFactory(provider.GetRequiredService<IHttpClientFactory>());

        factory.ProviderName.ShouldBe("Ollama");
    }

    [Fact]
    public void Should_CreateGenerator_When_BaseUrlIsConfigured()
    {
        var services = new ServiceCollection();
        services.AddOllamaEmbeddingProvider();
        var provider = services.BuildServiceProvider();
        var factory = new OllamaEmbeddingProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Model = "bge-m3",
            BaseUrl = "http://localhost:11434",
            Dimensions = 1024,
        };

        var generator = factory.Create(options);

        generator.Provider.ShouldBe("Ollama");
        generator.Model.ShouldBe("bge-m3");
        generator.Dimensions.ShouldBe(1024);
    }
}
