using Bogus;
using CodeRag.Infrastructure.Database.CodeQueries;
using Dapper;
using Pgvector;
using Shouldly;

namespace CodeRag.Infrastructure.Database.Tests;

[Collection(PostgresCollection.Name)]
public sealed class CodeDocumentsRepositoryTests(PostgresFixture fixture)
{
    private readonly Faker _faker = new();
    private readonly CodeDocumentsRepository _repository = new(fixture.DataSource);

    [Fact]
    public async Task Should_OrderResultsByDescendingSimilarity_When_MultipleDocumentsMatch()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "close-doc", new Vector(new float[] { 1f, 0f, 0f }));
        await InsertCodeDocumentAsync(projectId, modelId, "far-doc", new Vector(new float[] { 0f, 1f, 0f }));

        var results = (await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10)).ToArray();

        results.Length.ShouldBe(2);
        results[0].embeddingText.ShouldBe("close-doc");
        results[0].similarity.ShouldBeGreaterThan(results[1].similarity);
    }

    [Fact]
    public async Task Should_ExcludeDocumentsFromOtherProjects_When_Searching()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        var otherProjectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "own-doc", new Vector(new float[] { 1f, 0f, 0f }));
        await InsertCodeDocumentAsync(otherProjectId, modelId, "other-project-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10);

        results.ShouldAllBe(r => r.embeddingText == "own-doc");
    }

    [Fact]
    public async Task Should_ExcludeDocumentsFromOtherEmbeddingModels_When_Searching()
    {
        var model = UniqueModelName();
        var otherModel = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var otherModelId = await InsertEmbeddingModelAsync("OpenAI", otherModel, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "matching-model-doc", new Vector(new float[] { 1f, 0f, 0f }));
        await InsertCodeDocumentAsync(projectId, otherModelId, "other-model-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10);

        results.ShouldAllBe(r => r.embeddingText == "matching-model-doc");
    }

    [Fact]
    public async Task Should_RespectLimit_When_MoreDocumentsMatchThanLimit()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        for (var i = 0; i < 5; i++)
        {
            await InsertCodeDocumentAsync(projectId, modelId, $"doc-{i}", new Vector(new float[] { 1f, 0f, 0f }));
        }

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 2);

        results.Count().ShouldBe(2);
    }

    private string UniqueModelName() => $"bge-m3-{_faker.Random.AlphaNumeric(12)}";

    private async Task<long> InsertProjectAsync()
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO public.projects (name) VALUES (@name) RETURNING id",
            new { name = $"project-{_faker.Random.AlphaNumeric(12)}" });
    }

    private async Task<long> InsertEmbeddingModelAsync(string provider, string model, int dimensions)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO public.embedding_models (provider, model, dimensions, normalized)
            VALUES (@provider, @model, @dimensions, true)
            RETURNING id
            """,
            new { provider, model, dimensions });
    }

    private async Task InsertCodeDocumentAsync(long projectId, long embeddingModelId, string embeddingText, Vector embedding)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_documents (
                document_id, embedding_model_id, project_id, kind, embedding_text,
                embedding_text_hash, embedding, embedding_provider, embedding_dimensions,
                metadata, indexed_at)
            VALUES (
                @documentId, @embeddingModelId, @projectId, 'function', @embeddingText,
                @embeddingTextHash, @embedding, 'Ollama', 3,
                '{}'::jsonb, now())
            """,
            new
            {
                documentId = Guid.NewGuid().ToString(),
                embeddingModelId,
                projectId,
                embeddingText,
                embeddingTextHash = Guid.NewGuid().ToString("N"),
                embedding,
            });
    }
}
