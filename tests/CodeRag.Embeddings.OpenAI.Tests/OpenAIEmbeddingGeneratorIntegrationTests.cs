using CodeRag.Embeddings.Abstraction;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Shouldly;

namespace CodeRag.Embeddings.OpenAI.Tests;

/// <summary>
/// Exercises <see cref="OpenAIEmbeddingGenerator"/> against a real OpenAI-compatible server -
/// Hugging Face's Text Embeddings Inference, via Testcontainers - rather than OpenAI itself,
/// which would need credentials and network egress to a paid API; only that the server speaks
/// the same wire format actually matters here, and TEI does. Unlike
/// <see cref="OpenAIEmbeddingGeneratorTests"/>, which only proves we parse a response shape we
/// assumed was correct, this proves the request/response really work end to end.
/// </summary>
public sealed class OpenAIEmbeddingGeneratorIntegrationTests : IAsyncLifetime
{
    private const string Model = "sentence-transformers/all-MiniLM-L6-v2";
    private const int Dimensions = 384;
    private const int Port = 80;

    private readonly IContainer _container = new ContainerBuilder("ghcr.io/huggingface/text-embeddings-inference:cpu-1.7")
        .WithCommand("--model-id", Model)
        .WithPortBinding(Port, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(Port)))
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Should_ReturnRealEmbedding_When_CallingLiveOpenAICompatibleServer()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(Port)}/v1/"),
        };
        var options = new EmbeddingOptions { Provider = "OpenAI", Model = Model, Dimensions = Dimensions };
        var sut = new OpenAIEmbeddingGenerator(httpClient, options);

        var result = await sut.GenerateAsync("where is the discount logic?");

        result.values.Count.ShouldBe(Dimensions);
        result.values.ShouldContain(v => v != 0f);
    }
}
