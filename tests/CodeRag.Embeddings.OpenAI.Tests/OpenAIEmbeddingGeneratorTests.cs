using CodeRag.Embeddings.Abstraction;
using Shouldly;
using System.Net;

namespace CodeRag.Embeddings.OpenAI.Tests;

public sealed class OpenAIEmbeddingGeneratorTests
{
    [Fact]
    public async Task Should_ReturnEmbedding_When_ResponseIsSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(
            HttpStatusCode.OK,
            """{"data":[{"embedding":[0.1,0.2,0.3]}]}""");
        var sut = CreateSut(handler, model: "text-embedding-3-small", dimensions: 3);

        var result = await sut.GenerateAsync("where is the discount logic?");

        result.values.ShouldBe([0.1f, 0.2f, 0.3f]);
    }

    [Fact]
    public async Task Should_SendModelAndInput_When_GeneratingEmbedding()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"data":[{"embedding":[0.1]}]}""");
        var sut = CreateSut(handler, model: "text-embedding-3-small", dimensions: 1);

        await sut.GenerateAsync("where is the discount logic?");

        handler.LastRequestBody.ShouldNotBeNull();
        handler.LastRequestBody.ShouldContain("\"model\":\"text-embedding-3-small\"");
        handler.LastRequestBody.ShouldContain("\"input\":\"where is the discount logic?\"");
    }

    [Fact]
    public async Task Should_ThrowEmbeddingGenerationException_When_ServerReturnsError()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.Unauthorized, "{}");
        var sut = CreateSut(handler, model: "text-embedding-3-small", dimensions: 3);

        await Should.ThrowAsync<EmbeddingGenerationException>(() => sut.GenerateAsync("question"));
    }

    [Fact]
    public async Task Should_ThrowEmbeddingGenerationException_When_ResponseHasNoData()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"data":[]}""");
        var sut = CreateSut(handler, model: "text-embedding-3-small", dimensions: 3);

        await Should.ThrowAsync<EmbeddingGenerationException>(() => sut.GenerateAsync("question"));
    }

    private static OpenAIEmbeddingGenerator CreateSut(HttpMessageHandler handler, string model, int dimensions)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new EmbeddingOptions { Provider = "OpenAI", Model = model, Dimensions = dimensions };
        return new OpenAIEmbeddingGenerator(httpClient, options);
    }
}
