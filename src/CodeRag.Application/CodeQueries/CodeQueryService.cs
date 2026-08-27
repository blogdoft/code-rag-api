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

    /// <summary>Upper bound accepted for an explicit <c>limit</c>, to keep a single query cheap.</summary>
    public const int MaxResultLimit = 50;

    /// <summary>Generous cap on a natural-language question; guards against embedding-cost abuse and token-limit overruns.</summary>
    public const int MaxQuestionLength = 1000;

    public async Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        int? limit = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return CodeQueryFailures.QuestionRequired;
        }

        if (question.Length > MaxQuestionLength)
        {
            return CodeQueryFailures.QuestionTooLong(MaxQuestionLength);
        }

        if (limit is < 1 or > MaxResultLimit)
        {
            return CodeQueryFailures.LimitOutOfRange(1, MaxResultLimit);
        }

        if (minSimilarity is < 0.0 or > 1.0)
        {
            return CodeQueryFailures.MinSimilarityOutOfRange;
        }

        var projectExists = await projectsRepository.ExistsAsync(projectId, cancellationToken);
        if (!projectExists)
        {
            return CodeQueryFailures.ProjectNotFound(projectId);
        }

        // Embedding generation and the similarity search below are infrastructure calls: on
        // failure they throw and are left to propagate as unhandled exceptions (-> 500), since
        // there is no client-facing recovery for them - only the failures above are
        // domain-modeled outcomes exposed by the OpenAPI contract (400 / 404).
        var queryEmbedding = await embeddingGenerator.GenerateAsync(question, cancellationToken);

        var results = await codeDocumentsRepository.SearchAsync(
            projectId,
            embeddingGenerator.Provider,
            embeddingGenerator.Model,
            embeddingGenerator.Dimensions,
            queryEmbedding.values,
            limit ?? ResultLimit,
            minSimilarity,
            cancellationToken);

        return Result<IEnumerable<CodeQueryResult>>.FromSuccess(results);
    }
}
