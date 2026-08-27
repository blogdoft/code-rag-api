using CodeRag.Embeddings.Abstraction;
using Shouldly;
using Testcontainers.Ollama;

namespace CodeRag.Embeddings.Ollama.Tests;

/// <summary>
/// Exercises <see cref="OllamaEmbeddingGenerator"/> against a real Ollama server (Testcontainers),
/// unlike <see cref="OllamaEmbeddingGeneratorTests"/>, which only proves we parse a response shape
/// we assumed was correct - this proves the request Ollama actually receives, and its actual
/// response, both really work.
/// </summary>
public sealed class OllamaEmbeddingGeneratorIntegrationTests : IAsyncLifetime
{
    // Smallest embedding model Ollama publishes (~46 MB), to keep the container pull fast.
    private const string Model = "all-minilm";
    private const int Dimensions = 384;

    private readonly OllamaContainer _container = new OllamaBuilder("ollama/ollama:0.6.6").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await _container.ExecAsync(["ollama", "pull", Model]);
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Should_ReturnRealEmbedding_When_CallingLiveOllamaServer()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(_container.GetBaseAddress()) };
        var options = new EmbeddingOptions { Provider = "Ollama", Model = Model, Dimensions = Dimensions };
        var sut = new OllamaEmbeddingGenerator(httpClient, options);

        var result = await sut.GenerateAsync("where is the discount logic?");

        result.values.Count.ShouldBe(Dimensions);
        result.values.ShouldContain(v => v != 0f);
    }

    [Fact]
    public async Task Should_ThrowEmbeddingGenerationException_When_ModelDoesNotExistOnLiveServer()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri(_container.GetBaseAddress()) };
        var options = new EmbeddingOptions { Provider = "Ollama", Model = "no-such-model", Dimensions = Dimensions };
        var sut = new OllamaEmbeddingGenerator(httpClient, options);

        await Should.ThrowAsync<EmbeddingGenerationException>(() => sut.GenerateAsync("question"));
    }
}
