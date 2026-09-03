using Bogus;
using Dapper;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
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

    [Fact]
    public async Task Should_ReturnProject_When_GettingProjectThatExists()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var id = await InsertProjectAsync(name);

        var response = await _client.GetAsync($"/api/v1/projects/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("id").GetInt64().ShouldBe(id);
        document.RootElement.GetProperty("name").GetString().ShouldBe(name);
        document.RootElement.GetProperty("git_url").ValueKind.ShouldBe(JsonValueKind.Null);
        document.RootElement.GetProperty("git_raw_url").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_GettingProjectThatDoesNotExist()
    {
        var response = await _client.GetAsync("/api/v1/projects/999999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var content = await response.Content.ReadAsByteArrayAsync();
        content.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_GettingProjectWithNonNumericId()
    {
        var response = await _client.GetAsync("/api/v1/projects/not-a-number");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_CreateProject_When_NameIsValidAndUnique()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";

        var response = await _client.PostAsJsonAsync("/api/v1/projects", new { name });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("name").GetString().ShouldBe(name);
        document.RootElement.GetProperty("id").GetInt64().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_CreateProject_When_GitUrlAndGitRawUrlAreProvided()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();

        var response = await _client.PostAsJsonAsync("/api/v1/projects", new { name, git_url = gitUrl, git_raw_url = gitRawUrl });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("git_url").GetString().ShouldBe(gitUrl);
        document.RootElement.GetProperty("git_raw_url").GetString().ShouldBe(gitRawUrl);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_CreatingProjectWithBlankGitUrl()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new { name = $"project-{_faker.Random.AlphaNumeric(10)}", git_url = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_CreatingProjectWithoutName()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/projects", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CreatingProjectWithDuplicateName()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(name);

        var response = await _client.PostAsJsonAsync("/api/v1/projects", new { name });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_CreatingProjectWithUnknownProperty()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/projects",
            new { name = $"project-{_faker.Random.AlphaNumeric(10)}", unexpected = "field" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_UpdateProject_When_NameIsValidAndUnique()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");
        var newName = $"project-{_faker.Random.AlphaNumeric(10)}";

        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{id}", new { name = newName });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("name").GetString().ShouldBe(newName);
    }

    [Fact]
    public async Task Should_UpdateProject_When_GitUrlAndGitRawUrlAreProvided()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");
        var newName = $"project-{_faker.Random.AlphaNumeric(10)}";
        var gitUrl = _faker.Internet.Url();
        var gitRawUrl = _faker.Internet.Url();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{id}",
            new { name = newName, git_url = gitUrl, git_raw_url = gitRawUrl });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("git_url").GetString().ShouldBe(gitUrl);
        document.RootElement.GetProperty("git_raw_url").GetString().ShouldBe(gitRawUrl);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_UpdatingProjectThatDoesNotExist()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/v1/projects/999999999",
            new { name = $"project-{_faker.Random.AlphaNumeric(10)}" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_UpdatingProjectWithDuplicateName()
    {
        var existingName = $"project-{_faker.Random.AlphaNumeric(10)}";
        await InsertProjectAsync(existingName);
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");

        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{id}", new { name = existingName });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Should_AllowRenamingProjectToItsOwnCurrentName()
    {
        var name = $"project-{_faker.Random.AlphaNumeric(10)}";
        var id = await InsertProjectAsync(name);

        var response = await _client.PutAsJsonAsync($"/api/v1/projects/{id}", new { name });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_DeleteProject_When_ProjectHasNoIndexedCodeDocuments()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");

        var response = await _client.DeleteAsync($"/api/v1/projects/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/v1/projects/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_DeletingProjectThatDoesNotExist()
    {
        var response = await _client.DeleteAsync("/api/v1/projects/999999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DeletingProjectWithFeedback()
    {
        var id = await InsertProjectAsync($"project-{_faker.Random.AlphaNumeric(10)}");
        await InsertFeedbackAsync(id);

        var response = await _client.DeleteAsync($"/api/v1/projects/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    private async Task InsertFeedbackAsync(long projectId)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, username)
            VALUES (@projectId, 'where is X?', true, ARRAY[0.9]::float8[], 'claude code')
            """,
            new { projectId });
    }

    private async Task<long> InsertProjectAsync(string name)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name });
    }
}
