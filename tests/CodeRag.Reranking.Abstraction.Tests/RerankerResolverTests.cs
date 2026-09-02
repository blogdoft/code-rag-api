using CodeRag.Reranking.Abstraction;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CodeRag.Reranking.Abstraction.Tests;

public sealed class RerankerResolverTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_ReturnNoOpReranker_When_ProviderIsEmpty(string? provider)
    {
        var sut = CreateSut(provider!, []);

        var reranker = sut.Resolve();

        reranker.Provider.ShouldBe("None");
        reranker.CandidatePoolSize.ShouldBe(0);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Should_ReturnNoOpReranker_When_ProviderIsNone(string provider)
    {
        var sut = CreateSut(provider, []);

        var reranker = sut.Resolve();

        reranker.Provider.ShouldBe("None");
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ProviderIsUnknown()
    {
        var sut = CreateSut("Foo", []);

        Should.Throw<InvalidOperationException>(() => sut.Resolve());
    }

    [Fact]
    public void Should_ResolveMatchingFactory_When_ProviderIsConfigured()
    {
        var expectedReranker = Substitute.For<IReranker>();
        var factory = Substitute.For<IRerankerProviderFactory>();
        factory.ProviderName.Returns("Ollama");
        factory.Create(Arg.Any<RerankingOptions>()).Returns(expectedReranker);

        var sut = CreateSut("Ollama", [factory]);

        var reranker = sut.Resolve();

        reranker.ShouldBe(expectedReranker);
    }

    [Fact]
    public void Should_MatchProviderCaseInsensitively()
    {
        var expectedReranker = Substitute.For<IReranker>();
        var factory = Substitute.For<IRerankerProviderFactory>();
        factory.ProviderName.Returns("Ollama");
        factory.Create(Arg.Any<RerankingOptions>()).Returns(expectedReranker);

        var sut = CreateSut("OLLAMA", [factory]);

        var reranker = sut.Resolve();

        reranker.ShouldBe(expectedReranker);
    }

    private static RerankerResolver CreateSut(string provider, IEnumerable<IRerankerProviderFactory> factories) =>
        new(factories, Options.Create(new RerankingOptions { Provider = provider }));
}
