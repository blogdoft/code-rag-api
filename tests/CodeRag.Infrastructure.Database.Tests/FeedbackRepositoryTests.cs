using Bogus;
using CodeRag.Infrastructure.Database.Feedback;
using Dapper;
using Shouldly;

namespace CodeRag.Infrastructure.Database.Tests;

[Collection(PostgresCollection.Name)]
public sealed class FeedbackRepositoryTests(PostgresFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly FeedbackRepository _repository = new(fixture.DataSource);

    [Fact]
    public async Task Should_InsertAndReturnFeedback_When_ReasonIsProvided()
    {
        var projectId = await InsertProjectAsync();
        var similarities = new[] { 0.91, 0.73, 0.5 };

        var feedback = await _repository.InsertAsync(projectId, "why is this slow?", false, similarities, "not related", "claude code");

        feedback.Id.ShouldBeGreaterThan(0);
        feedback.ProjectId.ShouldBe(projectId);
        feedback.Question.ShouldBe("why is this slow?");
        feedback.Useful.ShouldBeFalse();
        feedback.Similarities.ShouldBe(similarities);
        feedback.Reason.ShouldBe("not related");
        feedback.User.ShouldBe("claude code");
        feedback.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Should_InsertAndReturnFeedback_When_ReasonIsOmittedAndSimilaritiesIsEmpty()
    {
        var projectId = await InsertProjectAsync();

        var feedback = await _repository.InsertAsync(projectId, "where is X?", true, [], null, "codex");

        feedback.Reason.ShouldBeNull();
        feedback.Similarities.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ProjectHasFeedback()
    {
        var projectId = await InsertProjectAsync();
        await _repository.InsertAsync(projectId, "where is X?", true, [0.9], null, "claude code");

        var exists = await _repository.ExistsForProjectAsync(projectId);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ProjectHasNoFeedback()
    {
        var projectId = await InsertProjectAsync();

        var exists = await _repository.ExistsForProjectAsync(projectId);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnDenseWeeklyGrid_When_FeedbackSkipsAWeek()
    {
        var projectId = await InsertProjectAsync();
        // Week 1: Mon 2020-01-06 - Sun 2020-01-12. Week 2 (2020-01-13 - 01-19): no feedback at
        // all. Week 3: Mon 2020-01-20 - Sun 2020-01-26. Dates fixed far in the past so no other
        // test in this shared-container collection (which always inserts feedback at "now") can
        // pollute this window.
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 6, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 8, 12, 0, 0, DateTimeKind.Utc), useful: false);
        await InsertFeedbackAtAsync(projectId, new DateTime(2020, 1, 20, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _repository.GetStatsAsync(
            new DateTime(2020, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 1, 26, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        weeks.Count.ShouldBe(3);

        var week1 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 6));
        week1.WeekEnd.ShouldBe(new DateOnly(2020, 1, 12));
        var week1Project = week1.Projects.Single(p => p.ProjectId == projectId);
        week1Project.TotalCount.ShouldBe(2);
        week1Project.UsefulCount.ShouldBe(1);
        week1Project.NotUsefulCount.ShouldBe(1);
        week1Project.UsefulPercentage.ShouldBe(50);
        week1Project.NotUsefulPercentage.ShouldBe(50);

        var week2 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 13));
        var week2Project = week2.Projects.Single(p => p.ProjectId == projectId);
        week2Project.TotalCount.ShouldBe(0);
        week2Project.UsefulPercentage.ShouldBe(0);
        week2Project.NotUsefulPercentage.ShouldBe(0);

        var week3 = weeks.Single(w => w.WeekStart == new DateOnly(2020, 1, 20));
        var week3Project = week3.Projects.Single(p => p.ProjectId == projectId);
        week3Project.TotalCount.ShouldBe(1);
        week3Project.UsefulCount.ShouldBe(1);
        week3Project.UsefulPercentage.ShouldBe(100);
    }

    [Fact]
    public async Task Should_ExcludeFeedback_When_CreatedAtIsOutsideTheRequestedWindow()
    {
        var projectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 1, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 22, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(projectId, new DateTime(2021, 3, 10, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _repository.GetStatsAsync(
            new DateTime(2021, 3, 8, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2021, 3, 14, 23, 59, 59, DateTimeKind.Utc),
            projectId);

        weeks.Count.ShouldBe(1);
        var project = weeks.Single().Projects.Single(p => p.ProjectId == projectId);
        project.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_RestrictToSingleProject_When_ProjectIdFilterIsGiven()
    {
        var filteredProjectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        await InsertFeedbackAtAsync(filteredProjectId, new DateTime(2022, 5, 4, 12, 0, 0, DateTimeKind.Utc), useful: true);
        await InsertFeedbackAtAsync(otherProjectId, new DateTime(2022, 5, 4, 12, 0, 0, DateTimeKind.Utc), useful: false);

        var weeks = await _repository.GetStatsAsync(
            new DateTime(2022, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2022, 5, 8, 23, 59, 59, DateTimeKind.Utc),
            filteredProjectId);

        var week = weeks.Single();
        week.Projects.ShouldHaveSingleItem();
        week.Projects[0].ProjectId.ShouldBe(filteredProjectId);
        week.Projects[0].TotalCount.ShouldBe(1);
        week.Projects[0].UsefulCount.ShouldBe(1);
    }

    [Fact]
    public async Task Should_IncludeEveryRegisteredProject_When_NoProjectIdFilterIsGiven()
    {
        var projectWithFeedback = await InsertProjectAsync();
        var projectWithoutFeedback = await InsertProjectAsync();
        await InsertFeedbackAtAsync(projectWithFeedback, new DateTime(2023, 9, 4, 12, 0, 0, DateTimeKind.Utc), useful: true);

        var weeks = await _repository.GetStatsAsync(
            new DateTime(2023, 9, 4, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2023, 9, 10, 23, 59, 59, DateTimeKind.Utc),
            null);

        var week = weeks.Single();
        week.Projects.ShouldContain(p => p.ProjectId == projectWithFeedback && p.TotalCount == 1);
        week.Projects.ShouldContain(p => p.ProjectId == projectWithoutFeedback && p.TotalCount == 0);
    }

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(10)}" });
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
