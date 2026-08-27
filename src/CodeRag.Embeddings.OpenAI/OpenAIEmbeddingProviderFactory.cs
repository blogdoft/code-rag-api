using CodeRag.Embeddings.Abstraction;
using System.Net.Http.Headers;

namespace CodeRag.Embeddings.OpenAI;

public sealed class OpenAIEmbeddingProviderFactory(IHttpClientFactory httpClientFactory) : IEmbeddingProviderFactory
{
    // Deliberate: this is only the fallback default - options.BaseUrl (below) already makes
    // the endpoint configurable, e.g. to point at an OpenAI-compatible server instead.
#pragma warning disable S1075
    private const string DefaultBaseUrl = "https://api.openai.com/v1/";
#pragma warning restore S1075

    public string ProviderName => "OpenAI";

    public IEmbeddingGenerator Create(EmbeddingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                $"'{EmbeddingOptions.SectionName}:{nameof(EmbeddingOptions.ApiKey)}' must be set when using the OpenAI embedding provider.");
        }

        var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        httpClient.BaseAddress = new Uri(options.BaseUrl ?? DefaultBaseUrl, UriKind.Absolute);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        return new OpenAIEmbeddingGenerator(httpClient, options);
    }
}
