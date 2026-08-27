using Bogus;
using CodeRag.Infrastructure.Database.Projects;
using Dapper;
using Shouldly;

namespace CodeRag.Infrastructure.Database.Tests;

[Collection(PostgresCollection.Name)]
public sealed class ProjectsRepositoryTests(PostgresFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly ProjectsRepository _repository = new(fixture.DataSource);

    [Fact]
    public async Task Should_ReturnOnlyMatchingProjects_When_NameFilterIsProvided()
    {
        var matchingName = $"shopping-cart-{_faker.Random.AlphaNumeric(10)}";
        var otherName = $"payments-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(matchingName);
        await InsertProjectAsync(otherName);

        var result = await _repository.SearchAsync("SHOPPING-CART");

        result.ShouldContain(p => p.name == matchingName);
        result.ShouldNotContain(p => p.name == otherName);
    }

    [Fact]
    public async Task Should_ReturnAllProjects_When_NameFilterIsNull()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(name);

        var result = await _repository.SearchAsync(null);

        result.ShouldContain(p => p.name == name);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoProjectMatchesFilter()
    {
        var result = await _repository.SearchAsync($"no-such-project-{_faker.Random.AlphaNumeric(16)}");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ProjectExists()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");

        var exists = await _repository.ExistsAsync(id);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ProjectDoesNotExist()
    {
        var exists = await _repository.ExistsAsync(-1);

        exists.ShouldBeFalse();
    }

    private async Task<long> InsertProjectAsync(string name)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name });
    }
}
