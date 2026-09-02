using CodeRag.Reranking.Abstraction;
using Shouldly;

namespace CodeRag.Reranking.Cohere.Tests;

public sealed class CohereRerankerTests
{
    [Fact]
    public async Task Should_MapResultsByIndexBackToCandidateIds()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(
            System.Net.HttpStatusCode.OK,
            """{"results":[{"index":1,"relevance_score":0.9},{"index":0,"relevance_score":0.2}]}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com") };
        var options = new RerankingOptions { Provider = "Cohere", Model = "rerank-english-v3.0" };
        var sut = new CohereReranker(httpClient, options);
        var candidates = new[] { new RerankCandidate(10, "first"), new RerankCandidate(20, "second") };

        var result = await sut.RerankAsync("question", candidates);

        result.ShouldBe([new RerankedCandidate(20, 0.9), new RerankedCandidate(10, 0.2)]);
    }
}
