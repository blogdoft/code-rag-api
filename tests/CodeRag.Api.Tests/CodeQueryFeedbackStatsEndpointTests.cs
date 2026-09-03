using Bogus;
using Dapper;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CodeQueryFeedbackStatsEndpointTests(ApiFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnOk_When_NoQueryParamsAreGiven()
    {
        var response = await _client.GetAsync("/api/v1/code-queries/feedback/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var start = document.RootElement.GetProperty("start_date").GetDateTime();
        var end = document.RootElement.GetProperty("end_date").GetDateTime();
        (end - start).ShouldBe(TimeSpan.FromDays(30));
        document.RootElement.GetProperty("weeks").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_ReturnDenseWeeklyGrid_When_ProjectIdFilterIsGiven()
    {
        var projectId = await InsertProjectAsync();

        // Fixed, far-past week so no other test in this shared-container collection (which
        // always inserts feedback "now" via POST .../feedback) can pollute this window.
        await InsertFeedbackAtAsync(projectId, new DateTime(2019, 6, 3, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2019, 6, 4, 12, 0, 0, DateTimeKind.Utc), useful: false);

        var response = await _client.GetAsync(
            $"/api/v1/code-queries/feedback/stats?start_date=2019-06-03T00:00:00Z&end_date=2019-06-09T23:59:59Z&project_id={projectId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var weeks = document.RootElement.GetProperty("weeks");
        weeks.GetArrayLength().ShouldBe(1);

        var week = weeks[0];
        week.GetProperty("week_start").GetString().ShouldBe("2019-06-03");
        week.GetProperty("week_end").GetString().ShouldBe("2019-06-09");
        var projects = week.GetProperty("projects");
        projects.GetArrayLength().ShouldBe(1);
        projects[0].GetProperty("project_id").GetInt64().ShouldBe(projectId);
        projects[0].GetProperty("total_count").GetInt64().ShouldBe(2);
        projects[0].GetProperty("useful_count").GetInt64().ShouldBe(1);
        projects[0].GetProperty("not_useful_count").GetInt64().ShouldBe(1);
        projects[0].GetProperty("useful_percentage").GetDouble().ShouldBe(50);
        projects[0].GetProperty("not_useful_percentage").GetDouble().ShouldBe(50);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_StartDateIsAfterEndDate()
    {
        var response = await _client.GetAsync(
            "/api/v1/code-queries/feedback/stats?start_date=2026-06-01T00:00:00Z&end_date=2026-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_WindowExceedsMaximum()
    {
        var response = await _client.GetAsync(
            "/api/v1/code-queries/feedback/stats?start_date=2020-01-01T00:00:00Z&end_date=2022-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectIdDoesNotExist()
    {
        var response = await _client.GetAsync("/api/v1/code-queries/feedback/stats?project_id=999999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(12)}" });
    }

    private async Task InsertFeedbackAtAsync(long projectId, DateTime createdAtUtc, bool useful)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, username, created_at)
            VALUES (@ProjectId, 'stats test', @Useful, ARRAY[]::float8[], 'tester', @CreatedAt)
            """,
            new { ProjectId = projectId, Useful = useful, CreatedAt = createdAtUtc });
    }
}
