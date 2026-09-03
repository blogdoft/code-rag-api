using Microsoft.Extensions.Options;
using Shouldly;

namespace CodeRag.Reranking.Abstraction.Tests;

public sealed class NoOpRerankerTests
{
    [Fact]
    public async Task Should_ReturnEveryCandidateUnscoredInOriginalOrder()
    {
        var resolver = new RerankerResolver([], Options.Create(new RerankingOptions { Provider = string.Empty }));
        var sut = resolver.Resolve();
        var candidates = new[]
        {
            new RerankCandidate(1, "first"),
            new RerankCandidate(2, "second"),
            new RerankCandidate(3, "third"),
        };

        var result = await sut.RerankAsync("some question", candidates);

        result.Select(r => r.Id).ShouldBe([1, 2, 3]);
        result.ShouldAllBe(r => r.Score == null);
    }
}
