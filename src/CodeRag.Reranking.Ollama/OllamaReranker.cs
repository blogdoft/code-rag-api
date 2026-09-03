using CodeRag.Reranking.Abstraction;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeRag.Reranking.Ollama;

/// <summary>
/// Reranks vector-search candidates by asking an Ollama chat/instruct model to grade each
/// candidate's relevance to the query on a 0-10 scale (pointwise), one <c>POST api/generate</c>
/// call per candidate, bounded by <see cref="RerankingOptions.MaxConcurrency"/>. Ollama has no
/// native cross-encoder/rerank endpoint, so this is LLM-prompt-based scoring, not a true
/// cross-encoder - it requires an instruct-capable model (not an embedding-only model like
/// bge-m3) to already be pulled on the target Ollama instance.
/// </summary>
public sealed class OllamaReranker : IReranker
{
    private static readonly JsonElement ScoreResponseFormat = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { score = new { type = "integer", minimum = 0, maximum = 10 } },
        required = new[] { "score" },
    });

    private readonly HttpClient _httpClient;
    private readonly RerankingOptions _options;
    private readonly ILogger<OllamaReranker> _logger;

    public OllamaReranker(HttpClient httpClient, RerankingOptions options, ILogger<OllamaReranker> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public string Provider => "Ollama";

    public int CandidatePoolSize => _options.CandidatePoolSize;

    public async Task<IReadOnlyList<RerankedCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));

        var scored = await Task.WhenAll(candidates.Select(async candidate =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var score = await ScoreOneAsync(query, candidate, cancellationToken);
                return new RerankedCandidate(candidate.Id, score);
            }
            finally
            {
                gate.Release();
            }
        }));

        return scored.OrderByDescending(candidate => candidate.Score ?? 0.0).ToList();
    }

    private static string BuildPrompt(string query, string candidateText) => $"""
        You are a precise relevance grader for a code search engine. Given a user's natural
        language question and a single candidate piece of source code, rate how well the
        candidate answers the question on an integer scale from 0 (irrelevant) to 10 (perfectly
        answers it). Respond with only the score.

        Question: {query}

        Candidate code:
        {candidateText}
        """;

    private async Task<double> ScoreOneAsync(string query, RerankCandidate candidate, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var request = new OllamaGenerateRequest(
                _options.Model,
                BuildPrompt(query, candidate.Text),
                ScoreResponseFormat,
                new OllamaGenerateOptions(0),
                false);

            using var response = await _httpClient.PostAsJsonAsync("api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(body?.Response))
            {
                throw new RerankingException("Ollama returned no response body while reranking.");
            }

            var payload = JsonSerializer.Deserialize<OllamaScorePayload>(body.Response)
                ?? throw new RerankingException("Ollama returned an unparseable relevance score.");

            var score = Math.Clamp(payload.Score, 0, 10) / 10.0;
            _logger.LogInformation(
                "Reranked candidate {CandidateId} in {ElapsedMilliseconds}ms with score {Score}",
                candidate.Id,
                stopwatch.ElapsedMilliseconds,
                score);
            return score;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "Failed to rerank candidate {CandidateId} using Ollama model '{Model}' after {ElapsedMilliseconds}ms",
                candidate.Id,
                _options.Model,
                stopwatch.ElapsedMilliseconds);
            throw new RerankingException($"Failed to rerank candidate {candidate.Id} using Ollama model '{_options.Model}'.", ex);
        }
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention (and every consumer's expectation) is
    // PascalCase.
#pragma warning disable SA1313
    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("format")] JsonElement Format,
        [property: JsonPropertyName("options")] OllamaGenerateOptions Options,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaGenerateOptions([property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OllamaGenerateResponse([property: JsonPropertyName("response")] string? Response);

    private sealed record OllamaScorePayload([property: JsonPropertyName("score")] int Score);
#pragma warning restore SA1313
}
