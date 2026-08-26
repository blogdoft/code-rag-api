using CodeRag.Embeddings.Abstraction;
using Shouldly;

namespace CodeRag.Embeddings.Local.Tests;

public sealed class LocalEmbeddingGeneratorTests
{
    [Fact]
    public void Should_ThrowInvalidOperationException_When_LocalModelPathIsNotConfigured()
    {
        var options = new EmbeddingOptions { Provider = "Local", Model = "bge-m3", Dimensions = 1024 };

        Should.Throw<InvalidOperationException>(() => new LocalEmbeddingGenerator(options));
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_ModelFilesDoNotExistAtPath()
    {
        var options = new EmbeddingOptions
        {
            Provider = "Local",
            Model = "bge-m3",
            Dimensions = 1024,
            LocalModelPath = Path.Combine(Path.GetTempPath(), $"no-such-model-{Guid.NewGuid():N}"),
        };

        Should.Throw<InvalidOperationException>(() => new LocalEmbeddingGenerator(options));
    }
}
