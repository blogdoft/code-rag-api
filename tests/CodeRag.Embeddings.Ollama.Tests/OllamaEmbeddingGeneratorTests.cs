using CodeRag.Embeddings.Abstraction;
using Shouldly;
using System.Net;

namespace CodeRag.Embeddings.Ollama.Tests;

public sealed class OllamaEmbeddingGeneratorTests
{
    [Fact]
    public async Task Should_ReturnEmbedding_When_ResponseIsSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.OK,
            """{"embeddings":[[0.1,0.2,0.3]]}""");
        var sut = CreateSut(handler, model: "bge-m3", dimensions: 3);

        var result = await sut.GenerateAsync("where is the discount logic?");

        result.values.ShouldBe([0.1f, 0.2f, 0.3f]);
    }

    [Fact]
    public async Task Should_SendModelAndInput_When_GeneratingEmbedding()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"embeddings":[[0.1]]}""");
        var sut = CreateSut(handler, model: "bge-m3", dimensions: 1);

        await sut.GenerateAsync("where is the discount logic?");

        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain("\"model\":\"bge-m3\"");
        handler.LastRequestBody.ShouldContain("\"input\":\"where is the discount logic?\"");
    }

    [Fact]
    public async Task Should_ThrowEmbeddingGenerationException_When_ServerReturnsError()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.InternalServerError, "{}");
        var sut = CreateSut(handler, model: "bge-m3", dimensions: 3);

        await Should.ThrowAsync<EmbeddingGenerationException>(() => sut.GenerateAsync("question"));
    }

    [Fact]
    public async Task Should_ThrowEmbeddingGenerationException_When_ResponseHasNoEmbeddings()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"embeddings":[]}""");
        var sut = CreateSut(handler, model: "bge-m3", dimensions: 3);

        await Should.ThrowAsync<EmbeddingGenerationException>(() => sut.GenerateAsync("question"));
    }

    private static OllamaEmbeddingGenerator CreateSut(HttpMessageHandler handler, string model, int dimensions)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = new EmbeddingOptions { Provider = "Ollama", Model = model, Dimensions = dimensions };
        return new OllamaEmbeddingGenerator(httpClient, options);
    }
}
