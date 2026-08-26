using Shouldly;

namespace CodeRag.Embeddings.Local.Tests;

public sealed class LocalEmbeddingProviderFactoryTests
{
    [Fact]
    public void Should_ReturnLocalProviderName()
    {
        new LocalEmbeddingProviderFactory().ProviderName.ShouldBe("Local");
    }
}
