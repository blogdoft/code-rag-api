using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnOk_When_Requested()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_NotBeListed_When_SwaggerDocumentIsRequested()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");

        document.GetProperty("paths").EnumerateObject()
            .ShouldNotContain(path => path.Name == "/health");
    }
}
