using System.Net;
using System.Text;

namespace CodeRag.Embeddings.OpenAI.Tests;

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public string? LastRequestBody { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json) => new(_ => new HttpResponseMessage(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return responder(request);
    }
}
