using Shouldly;
using System.Net;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CorsTests(ApiFixture fixture)
{
    /// <summary>Matches the default in <c>appsettings.json</c>'s <c>Cors:AllowedOrigins</c>.</summary>
    private const string AllowedOrigin = "http://localhost:4200";

    private const string DisallowedOrigin = "http://evil.example.com";

    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_AllowPreflight_When_OriginIsAllowed()
    {
        var response = await SendPreflightAsync(AllowedOrigin);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain(AllowedOrigin);
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().ShouldContain("GET");
    }

    [Fact]
    public async Task Should_NotIncludeAllowOriginHeader_When_PreflightOriginIsNotAllowed()
    {
        var response = await SendPreflightAsync(DisallowedOrigin);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Should_IncludeAllowOriginHeader_When_ActualRequestOriginIsAllowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain(AllowedOrigin);
    }

    [Fact]
    public async Task Should_NotIncludeAllowOriginHeader_When_ActualRequestOriginIsNotAllowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/projects");
        request.Headers.Add("Origin", DisallowedOrigin);

        var response = await _client.SendAsync(request);

        // The CORS middleware doesn't block the request server-side (that's the browser's job
        // once it sees no Access-Control-Allow-Origin header) - it only omits the header.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/projects");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return await _client.SendAsync(request);
    }
}
