namespace CodeRag.Reranking.Abstraction;

/// <summary>
/// Configuration bound from the "Reranking" configuration section. A single set of options
/// describes whichever provider is currently selected via <see cref="Provider"/>; providers
/// ignore the properties that do not apply to them. Reranking is optional: an empty (or
/// "None") <see cref="Provider"/> disables it entirely, with zero behavior/perf change.
/// </summary>
public sealed class RerankingOptions
{
    public const string SectionName = "Reranking";

    /// <summary>
    /// Name of the provider to use, matched against <see cref="IRerankerProviderFactory.ProviderName"/>
    /// (e.g. "Ollama", "Cohere"). Matching is case-insensitive. Empty or "None" disables reranking.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Name of the model to request from the provider.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Base URL of the provider's HTTP API.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>API key/token used to authenticate with the provider, when required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Request timeout, in seconds, for HTTP-based providers.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>How many top vector-search results to pull and rerank before truncating to the caller's limit.</summary>
    public int CandidatePoolSize { get; set; } = 25;

    /// <summary>Max number of concurrent scoring calls issued to the provider (pointwise strategies only).</summary>
    public int MaxConcurrency { get; set; } = 4;
}
