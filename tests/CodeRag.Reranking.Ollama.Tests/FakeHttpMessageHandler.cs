using System.Net;
using System.Text;

namespace CodeRag.Reranking.Ollama.Tests;

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<string> RequestBodies { get; } = [];

    public static FakeHttpMessageHandler ReturningJson(HttpStatusCode statusCode, string json) => new(_ => new HttpResponseMessage(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    });

    public static FakeHttpMessageHandler ReturningScore(int score) =>
        ReturningJson(HttpStatusCode.OK, $$"""{"response":"{\"score\":{{score}}}"}""");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return responder(request);
    }
}
