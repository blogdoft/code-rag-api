using Bogus;
using CodeRag.Application.CodeQueries;
using Dapper;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CodeQueriesEndpointTests(ApiFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnBadRequest_When_ProjectIdIsNotNumeric()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects/not-a-number/code-queries",
            new { question = "where is X?" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var detail = document.RootElement.GetProperty("detail").GetString();
        detail.ShouldNotBeNull();
        detail.ShouldContain("not-a-number");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects/999999999/code-queries",
            new { question = "where is X?" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_QuestionIsMissing()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries",
            new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_QuestionIsBlank()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries",
            new { question = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_QuestionExceedsMaxLength()
    {
        var projectId = await InsertProjectAsync();
        var tooLong = new string('a', CodeQueryService.MaxQuestionLength + 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries",
            new { question = tooLong });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_RequestBodyHasUnknownProperty()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries",
            new { question = "where is X?", unexpected = "field" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnServerErrorProblemDetails_When_EmbeddingProviderIsUnreachable()
    {
        // No Ollama server is running at the configured BaseUrl, so embedding generation throws
        // and should surface as the 500 shape from UnhandledExceptionFilter, not a raw 500.
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries",
            new { question = "where is the discount logic?" });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        var exception = document.RootElement.GetProperty("exception");
        exception.GetProperty("exception_type").GetString().ShouldNotBeNullOrEmpty();
        exception.GetProperty("message").GetString().ShouldNotBeNullOrEmpty();
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(12)}" });
    }
}
