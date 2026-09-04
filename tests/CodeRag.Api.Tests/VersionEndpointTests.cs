using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class VersionEndpointTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnVersion_When_Requested()
    {
        var response = await _client.GetAsync("/version");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("version").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
