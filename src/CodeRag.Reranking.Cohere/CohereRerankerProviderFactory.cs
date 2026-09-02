using CodeRag.Reranking.Abstraction;
using System.Net.Http.Headers;

namespace CodeRag.Reranking.Cohere;

public sealed class CohereRerankerProviderFactory(IHttpClientFactory httpClientFactory) : IRerankerProviderFactory
{
    private const string DefaultBaseUrl = "https://api.cohere.com";

    public string ProviderName => "Cohere";

    public IReranker Create(RerankingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                $"'{RerankingOptions.SectionName}:{nameof(RerankingOptions.ApiKey)}' must be set when using the Cohere reranking provider.");
        }

        var httpClient = httpClientFactory.CreateClient(ServiceCollectionExtensions.HttpClientName);
        httpClient.BaseAddress = new Uri(
            string.IsNullOrWhiteSpace(options.BaseUrl) ? DefaultBaseUrl : options.BaseUrl,
            UriKind.Absolute);
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        return new CohereReranker(httpClient, options);
    }
}
