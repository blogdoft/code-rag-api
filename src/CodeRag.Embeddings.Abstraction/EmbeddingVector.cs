namespace CodeRag.Embeddings.Abstraction;

/// <summary>
/// A dense vector produced by an embedding provider for a single piece of text.
/// </summary>
/// <param name="values">The raw embedding components, in provider-defined order.</param>
public sealed record EmbeddingVector(IReadOnlyList<float> values)
{
    public int Dimensions => values.Count;
}
