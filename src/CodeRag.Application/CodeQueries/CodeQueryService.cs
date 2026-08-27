using BlogDoFT.Libs.ResultPattern;
using CodeRag.Application.Projects;
using CodeRag.Embeddings.Abstraction;

namespace CodeRag.Application.CodeQueries;

public sealed class CodeQueryService(
    IProjectsRepository projectsRepository,
    ICodeDocumentsRepository codeDocumentsRepository,
    IEmbeddingGenerator embeddingGenerator) : ICodeQueryService
{
    /// <summary>Matches the <c>LIMIT 10</c> used in the reference similarity query.</summary>
    public const int ResultLimit = 10;

    public async Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return CodeQueryFailures.QuestionRequired;
        }

        var projectExists = await projectsRepository.ExistsAsync(projectId, cancellationToken);
        if (!projectExists)
        {
            return CodeQueryFailures.ProjectNotFound(projectId);
        }

        // Embedding generation and the similarity search below are infrastructure calls: on
        // failure they throw and are left to propagate as unhandled exceptions (-> 500), since
        // there is no client-facing recovery for them - only the two failures above are
        // domain-modeled outcomes exposed by the OpenAPI contract (400 / 404).
        var queryEmbedding = await embeddingGenerator.GenerateAsync(question, cancellationToken);

        var results = await codeDocumentsRepository.SearchAsync(
            projectId,
            embeddingGenerator.Provider,
            embeddingGenerator.Model,
            embeddingGenerator.Dimensions,
            queryEmbedding.values,
            ResultLimit,
            cancellationToken);

        return Result<IEnumerable<CodeQueryResult>>.FromSuccess(results);
    }
}
