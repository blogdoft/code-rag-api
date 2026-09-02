using CodeRag.Reranking.Abstraction;
using Shouldly;
using System.Net;

namespace CodeRag.Reranking.Ollama.Tests;

public sealed class OllamaRerankerTests
{
    [Fact]
    public async Task Should_ReturnNormalizedScore_When_ResponseIsSuccessful()
    {
        var handler = FakeHttpMessageHandler.ReturningScore(7);
        var sut = CreateSut(handler);

        var result = await sut.RerankAsync("where is the discount logic?", [new RerankCandidate(1, "some code")]);

        result.Single().Score.ShouldBe(0.7);
    }

    [Fact]
    public async Task Should_SortCandidatesDescendingByScore()
    {
        var scores = new Queue<int>([2, 9, 5]);
        var handler = new SequencedScoreHandler(scores);
        var sut = CreateSut(handler);
        var candidates = new[]
        {
            new RerankCandidate(1, "low"),
            new RerankCandidate(2, "high"),
            new RerankCandidate(3, "medium"),
        };

        var result = await sut.RerankAsync("question", candidates);

        result.Select(r => r.Id).ShouldBe([2, 3, 1]);
    }

    [Fact]
    public async Task Should_SendModelAndQuestionAndCandidateText_When_Reranking()
    {
        var handler = FakeHttpMessageHandler.ReturningScore(5);
        var sut = CreateSut(handler, model: "qwen2.5:7b-instruct");

        await sut.RerankAsync("where is the discount logic?", [new RerankCandidate(1, "def apply_discount(): pass")]);

        handler.RequestBodies.Single().ShouldContain("\"model\":\"qwen2.5:7b-instruct\"");
        handler.RequestBodies.Single().ShouldContain("where is the discount logic?");
        handler.RequestBodies.Single().ShouldContain("def apply_discount(): pass");
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_ServerReturnsError()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.InternalServerError, "{}");
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_ResponseHasNoScorePayload()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"response":""}""");
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public async Task Should_ThrowRerankingException_When_ResponseIsMalformedJson()
    {
        var handler = FakeHttpMessageHandler.ReturningJson(HttpStatusCode.OK, """{"response":"not json"}""");
        var sut = CreateSut(handler);

        await Should.ThrowAsync<RerankingException>(() => sut.RerankAsync("question", [new RerankCandidate(1, "code")]));
    }

    [Fact]
    public void Should_ReportCandidatePoolSizeFromOptions()
    {
        var sut = CreateSut(FakeHttpMessageHandler.ReturningScore(5), candidatePoolSize: 42);

        sut.CandidatePoolSize.ShouldBe(42);
    }

    private static OllamaReranker CreateSut(
        HttpMessageHandler handler,
        string model = "qwen2.5:7b-instruct",
        int candidatePoolSize = 25)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = new RerankingOptions { Provider = "Ollama", Model = model, CandidatePoolSize = candidatePoolSize, MaxConcurrency = 4 };
        return new OllamaReranker(httpClient, options);
    }

    private sealed class SequencedScoreHandler(Queue<int> scores) : HttpMessageHandler
    {
        private readonly object _lock = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int score;
            lock (_lock)
            {
                score = scores.Dequeue();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"response":"{\"score\":{{score}}}"}"""),
            });
        }
    }
}
