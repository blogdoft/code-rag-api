namespace CodeRag.Embeddings.Abstraction;

/// <summary>
/// Configuration bound from the "Embeddings" configuration section. A single set of
/// options describes whichever provider is currently selected via <see cref="Provider"/>;
/// providers ignore the properties that do not apply to them.
/// </summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>
    /// Name of the provider to use, matched against <see cref="IEmbeddingProviderFactory.ProviderName"/>
    /// (e.g. "Local", "Ollama", "OpenAI"). Matching is case-insensitive.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Name of the embedding model to request from the provider.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Base URL of the provider's HTTP API. Unused by the Local provider.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API key/token used to authenticate with the provider, when required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Expected dimensionality of vectors produced by <see cref="Model"/>.</summary>
    public int Dimensions { get; set; }

    /// <summary>Whether the provider returns L2-normalized vectors.</summary>
    public bool Normalized { get; set; }

    /// <summary>Filesystem path to the local ONNX model directory. Only used by the Local provider.</summary>
    public string? LocalModelPath { get; set; }

    /// <summary>Request timeout, in seconds, for HTTP-based providers.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
