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

    /// <summary>Matches the <c>maxLength</c> constraint on each filter's <c>value</c> field in the OpenAPI contract.</summary>
    public const int MaxFilterValueLength = 200;

    public async Task<Result<IEnumerable<CodeQueryResult>>> QueryAsync(
        long projectId,
        string? question,
        int? limit = null,
        double? minSimilarity = null,
        KindFilterOperator? kindOperator = null,
        string? kindValue = null,
        NamespaceFilterOperator? namespaceOperator = null,
        string? namespaceValue = null,
        TypeNameFilterOperator? typeNameOperator = null,
        string? typeNameValue = null,
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

        var filterValidationFailure =
            ValidateFilterValue(kindOperator, kindValue, CodeQueryFailures.KindFilterValueRequired, CodeQueryFailures.KindFilterValueTooLong)
            ?? ValidateFilterValue(namespaceOperator, namespaceValue, CodeQueryFailures.NamespaceFilterValueRequired, CodeQueryFailures.NamespaceFilterValueTooLong)
            ?? ValidateFilterValue(typeNameOperator, typeNameValue, CodeQueryFailures.TypeNameFilterValueRequired, CodeQueryFailures.TypeNameFilterValueTooLong);
        if (filterValidationFailure is not null)
        {
            return filterValidationFailure;
        }

        var project = await projectsRepository.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
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
            kindOperator,
            kindValue,
            namespaceOperator,
            namespaceValue,
            typeNameOperator,
            typeNameValue,
            cancellationToken);

        var resultsWithGitLinks = results.Select(result => result with
        {
            GitUrl = project.GitUrl,
            GitRawUrl = project.GitRawUrl is null || result.SourceFile is null
                ? null
                : $"{project.GitRawUrl}/{result.SourceFile}",
        });

        return Result<IEnumerable<CodeQueryResult>>.FromSuccess(resultsWithGitLinks);
    }

    // Value is only required/validated when the caller actually sets the operator - null operator
    // means "no filter", regardless of what value was passed alongside it.
    private static Failure? ValidateFilterValue<TOperator>(
        TOperator? filterOperator,
        string? value,
        Failure valueRequiredFailure,
        Func<int, Failure> valueTooLongFailure)
        where TOperator : struct
    {
        if (filterOperator is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return valueRequiredFailure;
        }

        return value.Length > MaxFilterValueLength ? valueTooLongFailure(MaxFilterValueLength) : null;
    }
}
