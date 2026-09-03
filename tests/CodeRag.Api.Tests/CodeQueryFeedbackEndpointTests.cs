using Bogus;
using CodeRag.Application.Feedback;
using Dapper;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CodeQueryFeedbackEndpointTests(ApiFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnCreated_When_RequestIsValid()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = new[] { 0.9, 0.7 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("id").GetInt64().ShouldBeGreaterThan(0);
        document.RootElement.GetProperty("project_id").GetInt64().ShouldBe(projectId);
        document.RootElement.GetProperty("question").GetString().ShouldBe("where is X?");
        document.RootElement.GetProperty("useful").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("similarities").EnumerateArray().Select(e => e.GetDouble()).ShouldBe([0.9, 0.7]);
        document.RootElement.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);
        document.RootElement.GetProperty("user").GetString().ShouldBe("claude code");
        document.RootElement.TryGetProperty("created_at", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnCreated_When_ReasonAndEmptySimilaritiesAreProvided()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = false, similarities = Array.Empty<double>(), reason = "no results", user = "ftathiago" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("similarities").GetArrayLength().ShouldBe(0);
        document.RootElement.GetProperty("reason").GetString().ShouldBe("no results");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_ProjectIdIsNotNumeric()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects/not-a-number/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = new[] { 0.9 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects/999999999/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = new[] { 0.9 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_QuestionIsMissing()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { useful = true, similarities = new[] { 0.9 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_QuestionExceedsMaxLength()
    {
        var projectId = await InsertProjectAsync();
        var tooLong = new string('a', FeedbackService.MaxQuestionLength + 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = tooLong, useful = true, similarities = new[] { 0.9 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_UsefulIsMissing()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", similarities = new[] { 0.9 }, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_SimilaritiesIsMissing()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = true, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_TooManySimilaritiesAreProvided()
    {
        var projectId = await InsertProjectAsync();
        var tooMany = Enumerable.Repeat(0.5, FeedbackService.MaxSimilaritiesCount + 1).ToArray();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = tooMany, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_UserIsMissing()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = new[] { 0.9 } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_UserExceedsMaxLength()
    {
        var projectId = await InsertProjectAsync();
        var tooLong = new string('a', FeedbackService.MaxUserLength + 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = true, similarities = new[] { 0.9 }, user = tooLong });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_ReasonExceedsMaxLength()
    {
        var projectId = await InsertProjectAsync();
        var tooLong = new string('a', FeedbackService.MaxReasonLength + 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new { question = "where is X?", useful = false, similarities = new[] { 0.2 }, reason = tooLong, user = "claude code" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_RequestBodyHasUnknownProperty()
    {
        var projectId = await InsertProjectAsync();

        var response = await _client.PostAsync(
            $"/api/v1/projects/{projectId}/code-queries/feedback",
            new StringContent(
                """{"question":"where is X?","useful":true,"similarities":[0.9],"user":"claude code","unexpected":"field"}""",
                Encoding.UTF8,
                "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(12)}" });
    }
}
