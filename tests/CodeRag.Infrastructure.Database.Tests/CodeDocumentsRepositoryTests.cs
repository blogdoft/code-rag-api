using Bogus;
using CodeRag.Application.CodeQueries;
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

        var results = (await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10, null)).ToArray();

        results.Length.ShouldBe(2);
        results[0].EmbeddingText.ShouldBe("close-doc");
        results[0].Similarity.ShouldBeGreaterThan(results[1].Similarity);
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

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10, null);

        results.ShouldAllBe(r => r.EmbeddingText == "own-doc");
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

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10, null);

        results.ShouldAllBe(r => r.EmbeddingText == "matching-model-doc");
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

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 2, null);

        results.Count().ShouldBe(2);
    }

    [Fact]
    public async Task Should_ExcludeResultsBelowMinSimilarity_When_MinSimilarityIsSet()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "identical-doc", new Vector(new float[] { 1f, 0f, 0f }));
        await InsertCodeDocumentAsync(projectId, modelId, "orthogonal-doc", new Vector(new float[] { 0f, 1f, 0f }));

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10, 0.5);

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("identical-doc");
    }

    [Fact]
    public async Task Should_ReturnUnfilteredResults_When_MinSimilarityIsNull()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "identical-doc", new Vector(new float[] { 1f, 0f, 0f }));
        await InsertCodeDocumentAsync(projectId, modelId, "orthogonal-doc", new Vector(new float[] { 0f, 1f, 0f }));

        var results = await _repository.SearchAsync(projectId, "Ollama", model, 3, new float[] { 1f, 0f, 0f }, 10, null);

        results.Count().ShouldBe(2);
    }

    [Theory]
    [InlineData(KindFilterOperator.Contains, "*fun*", "function")]
    [InlineData(KindFilterOperator.Equals, "function", "function")]
    [InlineData(KindFilterOperator.NotEquals, "class", "function")]
    public async Task Should_ReturnMatchingDocuments_When_KindFilterIsApplied(KindFilterOperator op, string value, string expected)
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "function-doc", new Vector(new float[] { 1f, 0f, 0f }), kind: "function");
        await InsertCodeDocumentAsync(projectId, modelId, "class-doc", new Vector(new float[] { 1f, 0f, 0f }), kind: "class");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            kindOperator: op,
            kindValue: value);

        results.ShouldAllBe(r => r.Kind == expected);
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_MatchExactlyOnly_When_KindContainsValueHasNoWildcard()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "function-doc", new Vector(new float[] { 1f, 0f, 0f }), kind: "function");
        await InsertCodeDocumentAsync(projectId, modelId, "functional-doc", new Vector(new float[] { 1f, 0f, 0f }), kind: "functional");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            kindOperator: KindFilterOperator.Contains,
            kindValue: "function");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("function-doc");
    }

    [Fact]
    public async Task Should_MatchPrefix_When_NamespaceContainsValueEndsWithWildcard()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "shop-billing-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Shop.Billing");
        await InsertCodeDocumentAsync(projectId, modelId, "other-namespace-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Other.Namespace");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            namespaceOperator: NamespaceFilterOperator.Contains,
            namespaceValue: "Shop.*");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("shop-billing-doc");
    }

    [Fact]
    public async Task Should_MatchSuffix_When_TypeNameContainsValueStartsWithWildcard()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "order-controller-doc", new Vector(new float[] { 1f, 0f, 0f }), typeName: "OrderController");
        await InsertCodeDocumentAsync(projectId, modelId, "controller-helper-doc", new Vector(new float[] { 1f, 0f, 0f }), typeName: "ControllerHelper");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            typeNameOperator: TypeNameFilterOperator.Contains,
            typeNameValue: "*Controller");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("order-controller-doc");
    }

    [Fact]
    public async Task Should_IncludeDocumentWithNullNamespace_When_NamespaceFilterOperatorIsNotContains()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "billing-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Billing");
        await InsertCodeDocumentAsync(projectId, modelId, "null-namespace-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var results = (await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            namespaceOperator: NamespaceFilterOperator.NotContains,
            namespaceValue: "Billing")).ToArray();

        results.ShouldContain(r => r.EmbeddingText == "null-namespace-doc");
        results.ShouldNotContain(r => r.EmbeddingText == "billing-doc");
    }

    [Fact]
    public async Task Should_IncludeDocumentWithNullNamespace_When_NamespaceFilterOperatorIsNotEquals()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "billing-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Billing");
        await InsertCodeDocumentAsync(projectId, modelId, "null-namespace-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var results = (await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            namespaceOperator: NamespaceFilterOperator.NotEquals,
            namespaceValue: "Billing")).ToArray();

        results.ShouldContain(r => r.EmbeddingText == "null-namespace-doc");
        results.ShouldNotContain(r => r.EmbeddingText == "billing-doc");
    }

    [Fact]
    public async Task Should_ReturnOnlyMatchingNamespace_When_NamespaceFilterOperatorIsContains()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "billing-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Shop.Billing");
        await InsertCodeDocumentAsync(projectId, modelId, "catalog-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Shop.Catalog");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            namespaceOperator: NamespaceFilterOperator.Contains,
            namespaceValue: "*Billing*");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("billing-doc");
    }

    [Fact]
    public async Task Should_ReturnOnlyMatchingNamespace_When_NamespaceFilterOperatorIsEquals()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "billing-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Shop.Billing");
        await InsertCodeDocumentAsync(projectId, modelId, "catalog-doc", new Vector(new float[] { 1f, 0f, 0f }), @namespace: "Shop.Catalog");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            namespaceOperator: NamespaceFilterOperator.Equals,
            namespaceValue: "Shop.Billing");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("billing-doc");
    }

    [Fact]
    public async Task Should_IncludeDocumentWithNullTypeName_When_TypeNameFilterOperatorIsNotContains()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "controller-doc", new Vector(new float[] { 1f, 0f, 0f }), typeName: "OrderController");
        await InsertCodeDocumentAsync(projectId, modelId, "null-type-name-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var results = (await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            typeNameOperator: TypeNameFilterOperator.NotContains,
            typeNameValue: "*Controller*")).ToArray();

        results.ShouldContain(r => r.EmbeddingText == "null-type-name-doc");
        results.ShouldNotContain(r => r.EmbeddingText == "controller-doc");
    }

    [Fact]
    public async Task Should_ReturnOnlyMatchingTypeName_When_TypeNameFilterOperatorIsEquals()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "order-controller-doc", new Vector(new float[] { 1f, 0f, 0f }), typeName: "OrderController");
        await InsertCodeDocumentAsync(projectId, modelId, "cart-controller-doc", new Vector(new float[] { 1f, 0f, 0f }), typeName: "CartController");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            typeNameOperator: TypeNameFilterOperator.Equals,
            typeNameValue: "OrderController");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("order-controller-doc");
    }

    [Fact]
    public async Task Should_CombineKindNamespaceAndTypeNameFilters_When_AllAreApplied()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(
            projectId,
            modelId,
            "matching-doc",
            new Vector(new float[] { 1f, 0f, 0f }),
            kind: "method",
            @namespace: "Shop.Billing",
            typeName: "InvoiceService");
        await InsertCodeDocumentAsync(
            projectId,
            modelId,
            "wrong-kind-doc",
            new Vector(new float[] { 1f, 0f, 0f }),
            kind: "class",
            @namespace: "Shop.Billing",
            typeName: "InvoiceService");
        await InsertCodeDocumentAsync(
            projectId,
            modelId,
            "wrong-namespace-doc",
            new Vector(new float[] { 1f, 0f, 0f }),
            kind: "method",
            @namespace: "Shop.Catalog",
            typeName: "InvoiceService");

        var results = await _repository.SearchAsync(
            projectId,
            "Ollama",
            model,
            3,
            new float[] { 1f, 0f, 0f },
            10,
            null,
            kindOperator: KindFilterOperator.Equals,
            kindValue: "method",
            namespaceOperator: NamespaceFilterOperator.Equals,
            namespaceValue: "Shop.Billing",
            typeNameOperator: TypeNameFilterOperator.Equals,
            typeNameValue: "InvoiceService");

        var result = results.ShouldHaveSingleItem();
        result.EmbeddingText.ShouldBe("matching-doc");
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ProjectHasIndexedCodeDocuments()
    {
        var model = UniqueModelName();
        var modelId = await InsertEmbeddingModelAsync("Ollama", model, 3);
        var projectId = await InsertProjectAsync();
        await InsertCodeDocumentAsync(projectId, modelId, "some-doc", new Vector(new float[] { 1f, 0f, 0f }));

        var exists = await _repository.ExistsForProjectAsync(projectId);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ProjectHasNoIndexedCodeDocuments()
    {
        var projectId = await InsertProjectAsync();

        var exists = await _repository.ExistsForProjectAsync(projectId);

        exists.ShouldBeFalse();
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

    private async Task InsertCodeDocumentAsync(
        long projectId,
        long embeddingModelId,
        string embeddingText,
        Vector embedding,
        string kind = "function",
        string? @namespace = null,
        string? typeName = null)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO public.code_documents (
                document_id, embedding_model_id, project_id, kind, namespace, type_name, embedding_text,
                embedding_text_hash, embedding, embedding_provider, embedding_dimensions,
                metadata, indexed_at)
            VALUES (
                @documentId, @embeddingModelId, @projectId, @kind, @namespace, @typeName, @embeddingText,
                @embeddingTextHash, @embedding, 'Ollama', 3,
                '{}'::jsonb, now())
            """,
            new
            {
                documentId = Guid.NewGuid().ToString(),
                embeddingModelId,
                projectId,
                kind,
                @namespace,
                typeName,
                embeddingText,
                embeddingTextHash = Guid.NewGuid().ToString("N"),
                embedding,
            });
    }
}
