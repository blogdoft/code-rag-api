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

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(10)}" });
    }
}
