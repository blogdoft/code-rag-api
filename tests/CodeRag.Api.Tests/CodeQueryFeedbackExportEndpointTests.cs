using System.Globalization;
using System.Net;
using Bogus;
using CsvHelper;
using Dapper;
using Shouldly;

namespace CodeRag.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class CodeQueryFeedbackExportEndpointTests(ApiFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Should_ReturnOk_When_NoQueryParamsAreGiven()
    {
        var response = await _client.GetAsync("/api/v1/code-queries/feedback/export");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition?.FileName.ShouldNotBeNullOrEmpty();
        var rows = await ReadCsvRowsAsync(response);
        rows.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_ReturnRowsWithinWindow_When_DateRangeIsGiven()
    {
        var projectId = await InsertProjectAsync();
        // Fixed, far-past window so no other test in this shared-container collection can pollute it.
        await InsertFeedbackAtAsync(projectId, new DateTime(2018, 4, 5, 12, 0, 0, DateTimeKind.Utc), useful: true, similarities: [0.91, 0.73], reason: null);
        await InsertFeedbackAtAsync(projectId, new DateTime(2018, 4, 10, 12, 0, 0, DateTimeKind.Utc), useful: false, similarities: [], reason: "not related");

        var response = await _client.GetAsync(
            $"/api/v1/code-queries/feedback/export?start_date=2018-04-01T00:00:00Z&end_date=2018-04-30T23:59:59Z&project_id={projectId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await ReadCsvRowsAsync(response);
        rows.Count.ShouldBe(2);

        rows[0]["project_id"].ShouldBe(projectId.ToString(CultureInfo.InvariantCulture));
        rows[0]["useful"].ShouldBe("True");
        rows[0]["similarities"].ShouldBe("[0.91,0.73]");
        rows[0]["reason"].ShouldBeNullOrEmpty();
        rows[0]["created_at"].ShouldBe("2018-04-05T12:00:00Z");

        rows[1]["useful"].ShouldBe("False");
        rows[1]["similarities"].ShouldBe("[]");
        rows[1]["reason"].ShouldBe("not related");
        rows[1]["created_at"].ShouldBe("2018-04-10T12:00:00Z");
    }

    [Fact]
    public async Task Should_EscapeCommasQuotesAndNewlines_When_QuestionAndReasonContainThem()
    {
        var projectId = await InsertProjectAsync();
        await InsertFeedbackAsync(
            projectId,
            new DateTime(2018, 5, 5, 12, 0, 0, DateTimeKind.Utc),
            question: "Where is the \"stats\" endpoint, and how does it work?",
            reason: "Missing the join logic,\nplus other issues");

        var response = await _client.GetAsync(
            $"/api/v1/code-queries/feedback/export?start_date=2018-05-01T00:00:00Z&end_date=2018-05-31T23:59:59Z&project_id={projectId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = await ReadCsvRowsAsync(response);
        rows.ShouldHaveSingleItem();
        rows[0]["question"].ShouldBe("Where is the \"stats\" endpoint, and how does it work?");
        rows[0]["reason"].ShouldBe("Missing the join logic,\nplus other issues");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_StartDateIsAfterEndDate()
    {
        var response = await _client.GetAsync(
            "/api/v1/code-queries/feedback/export?start_date=2026-06-01T00:00:00Z&end_date=2026-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_WindowExceedsMaximum()
    {
        var response = await _client.GetAsync(
            "/api/v1/code-queries/feedback/export?start_date=2020-01-01T00:00:00Z&end_date=2022-01-01T00:00:00Z");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ProjectIdDoesNotExist()
    {
        var response = await _client.GetAsync("/api/v1/code-queries/feedback/export?project_id=999999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    private static async Task<IReadOnlyList<Dictionary<string, string>>> ReadCsvRowsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var rows = new List<Dictionary<string, string>>();
        await csv.ReadAsync();
        csv.ReadHeader();
        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();
            foreach (var header in csv.HeaderRecord!)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(12)}" });
    }

    private async Task InsertFeedbackAtAsync(long projectId, DateTime createdAtUtc, bool useful, double[] similarities, string? reason)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username, created_at)
            VALUES (@ProjectId, 'export test', @Useful, @Similarities, @Reason, 'tester', @CreatedAt)
            """,
            new { ProjectId = projectId, Useful = useful, Similarities = similarities, Reason = reason, CreatedAt = createdAtUtc });
    }

    private async Task InsertFeedbackAsync(long projectId, DateTime createdAtUtc, string question, string reason)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username, created_at)
            VALUES (@ProjectId, @Question, false, ARRAY[]::float8[], @Reason, 'tester', @CreatedAt)
            """,
            new { ProjectId = projectId, Question = question, Reason = reason, CreatedAt = createdAtUtc });
    }
}
