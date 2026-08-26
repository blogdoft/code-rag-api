using CodeRag.Embeddings.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CodeRag.Embeddings.OpenAI.Tests;

public sealed class OpenAIEmbeddingProviderFactoryTests
{
    [Fact]
    public void Should_ThrowInvalidOperationException_When_ApiKeyIsNotConfigured()
    {
        var factory = CreateFactory();
        var options = new EmbeddingOptions { Provider = "OpenAI", Model = "text-embedding-3-small", Dimensions = 1536 };

        Should.Throw<InvalidOperationException>(() => factory.Create(options));
    }

    [Fact]
    public void Should_ReturnOpenAIProviderName()
    {
        CreateFactory().ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public void Should_CreateGenerator_When_ApiKeyIsConfigured()
    {
        var factory = CreateFactory();
        var options = new EmbeddingOptions
        {
            Provider = "OpenAI",
            Model = "text-embedding-3-small",
            ApiKey = "sk-test",
            Dimensions = 1536,
        };

        var generator = factory.Create(options);

        generator.Provider.ShouldBe("OpenAI");
        generator.Model.ShouldBe("text-embedding-3-small");
        generator.Dimensions.ShouldBe(1536);
    }

    private static OpenAIEmbeddingProviderFactory CreateFactory()
    {
        var services = new ServiceCollection();
        services.AddOpenAIEmbeddingProvider();
        var provider = services.BuildServiceProvider();
        return new OpenAIEmbeddingProviderFactory(provider.GetRequiredService<IHttpClientFactory>());
    }
}
