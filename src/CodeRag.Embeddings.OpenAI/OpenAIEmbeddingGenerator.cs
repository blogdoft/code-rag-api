using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodeRag.Embeddings.Abstraction;

namespace CodeRag.Embeddings.OpenAI;

/// <summary>
/// Generates embeddings by calling OpenAI's <c>POST /embeddings</c> endpoint. Also works
/// against any OpenAI-compatible server (e.g. Text Embeddings Inference, vLLM, LM Studio)
/// since they implement the same wire format.
/// </summary>
public sealed class OpenAIEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;

    public OpenAIEmbeddingGenerator(HttpClient httpClient, EmbeddingOptions options)
    {
        _httpClient = httpClient;
        Model = options.Model;
        Dimensions = options.Dimensions;
    }

    public string Provider => "OpenAI";

    public string Model { get; }

    public int Dimensions { get; }

    public async Task<EmbeddingVector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync("embeddings", new OpenAIEmbeddingRequest(Model, text), cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var body = await response.Content
                .ReadFromJsonAsync<OpenAIEmbeddingResponse>(cancellationToken)
                .ConfigureAwait(false);

            var embedding = body?.Data?.FirstOrDefault()?.Embedding
                ?? throw new EmbeddingGenerationException("OpenAI returned no embeddings in its response.");

            return new EmbeddingVector(embedding);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            throw new EmbeddingGenerationException($"Failed to generate an embedding using OpenAI model '{Model}'.", ex);
        }
    }

    private sealed record OpenAIEmbeddingRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input);

    private sealed record OpenAIEmbeddingResponse(
        [property: JsonPropertyName("data")] List<OpenAIEmbeddingData>? Data);

    private sealed record OpenAIEmbeddingData(
        [property: JsonPropertyName("embedding")] float[] Embedding);
}
