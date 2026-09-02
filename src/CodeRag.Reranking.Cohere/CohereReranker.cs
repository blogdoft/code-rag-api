using CodeRag.Reranking.Abstraction;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CodeRag.Reranking.Cohere;

/// <summary>
/// Reranks vector-search candidates using Cohere's Rerank API (<c>POST v2/rerank</c>), which is
/// natively listwise: unlike the Ollama pointwise strategy, all candidates are scored in a
/// single call. This provider is registered but not wired into the default configuration - it
/// exists so a hosted reranking backend can be selected later via <c>Reranking:Provider</c>
/// without any change to the resolver.
/// </summary>
public sealed class CohereReranker : IReranker
{
    private readonly HttpClient _httpClient;
    private readonly RerankingOptions _options;

    public CohereReranker(HttpClient httpClient, RerankingOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public string Provider => "Cohere";

    public int CandidatePoolSize => _options.CandidatePoolSize;

    public async Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CohereRerankRequest(
                _options.Model,
                query,
                candidates.Select(candidate => candidate.Text).ToArray(),
                candidates.Count);

            using var response = await _httpClient.PostAsJsonAsync("v2/rerank", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CohereRerankResponse>(cancellationToken)
                ?? throw new RerankingException("Cohere returned no response body while reranking.");

            return body.Results
                .Select(result => new RerankedCandidate(candidates[result.Index].Id, result.RelevanceScore))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            throw new RerankingException($"Failed to rerank candidates using Cohere model '{_options.Model}'.", ex);
        }
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention (and every consumer's expectation) is
    // PascalCase.
#pragma warning disable SA1313
    private sealed record CohereRerankRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("documents")] string[] Documents,
        [property: JsonPropertyName("top_n")] int TopN);

    private sealed record CohereRerankResponse(
        [property: JsonPropertyName("results")] List<CohereRerankResult> Results);

    private sealed record CohereRerankResult(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("relevance_score")] double RelevanceScore);
#pragma warning restore SA1313
}
