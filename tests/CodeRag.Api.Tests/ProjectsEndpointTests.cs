using Bogus;
using Dapper;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class ProjectsEndpointTests(ApiFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnEmptyArray_When_NoProjectMatchesFilter()
    {
        var response = await _client.GetAsync($"/api/v1/projects?name=no-such-project-{_faker.Random.AlphaNumeric(16)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_ReturnMatchingProject_When_NameFilterMatches()
    {
        var name = $"shopping-cart-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(name);

        var response = await _client.GetAsync($"/api/v1/projects?name={name}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var project = document.RootElement.EnumerateArray().Single();
        project.GetProperty("name").GetString().ShouldBe(name);
        project.GetProperty("id").GetInt64().ShouldBeGreaterThan(0);
        project.TryGetProperty("created_at", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_NameFilterExceedsMaxLength()
    {
        var tooLong = new string('a', 201);

        var response = await _client.GetAsync($"/api/v1/projects?name={tooLong}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_NameFilterIsEmpty()
    {
        var response = await _client.GetAsync("/api/v1/projects?name=");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task InsertProjectAsync(string name)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO public.projects (name) VALUES (@name)", new { name });
    }
}
