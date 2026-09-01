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

        result.ShouldContain(p => p.Name == matchingName);
        result.ShouldNotContain(p => p.Name == otherName);
    }

    [Fact]
    public async Task Should_ReturnAllProjects_When_NameFilterIsNull()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(name);

        var result = await _repository.SearchAsync(null);

        result.ShouldContain(p => p.Name == name);
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

    [Fact]
    public async Task Should_ReturnProject_When_GettingProjectThatExists()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var id = await InsertProjectAsync(name);

        var project = await _repository.GetByIdAsync(id);

        project.ShouldNotBeNull();
        project.Name.ShouldBe(name);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GettingProjectThatDoesNotExist()
    {
        var project = await _repository.GetByIdAsync(-1);

        project.ShouldBeNull();
    }

    [Fact]
    public async Task Should_InsertAndReturnProject_When_Creating()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";

        var project = await _repository.InsertAsync(name, null, null);

        project.Id.ShouldBeGreaterThan(0);
        project.Name.ShouldBe(name);
        project.GitUrl.ShouldBeNull();
        project.GitRawUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Should_InsertAndReturnProject_When_GitFieldsAreProvided()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();

        var project = await _repository.InsertAsync(name, gitUrl, gitRawUrl);

        project.GitUrl.ShouldBe(gitUrl);
        project.GitRawUrl.ShouldBe(gitRawUrl);
    }

    [Fact]
    public async Task Should_UpdateAndReturnProject_When_ProjectExists()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");
        var newName = $"project-{_faker.Random.AlphaNumeric(10)}";
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();

        var project = await _repository.UpdateAsync(id, newName, gitUrl, gitRawUrl);

        project.ShouldNotBeNull();
        project.Name.ShouldBe(newName);
        project.GitUrl.ShouldBe(gitUrl);
        project.GitRawUrl.ShouldBe(gitRawUrl);
    }

    [Fact]
    public async Task Should_ReturnNull_When_UpdatingProjectThatDoesNotExist()
    {
        var project = await _repository.UpdateAsync(-1, $"project-{_faker.Random.AlphaNumeric(10)}", null, null);

        project.ShouldBeNull();
    }

    [Fact]
    public async Task Should_DeleteProject_When_ProjectExists()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");

        var deleted = await _repository.DeleteAsync(id);

        deleted.ShouldBeTrue();
        (await _repository.GetByIdAsync(id)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_DeletingProjectThatDoesNotExist()
    {
        var deleted = await _repository.DeleteAsync(-1);

        deleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_NameAlreadyExists()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(name);

        var exists = await _repository.NameExistsAsync(name);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_NameExistsOnlyOnTheExcludedProject()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var id = await InsertProjectAsync(name);

        var exists = await _repository.NameExistsAsync(name, id);

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
