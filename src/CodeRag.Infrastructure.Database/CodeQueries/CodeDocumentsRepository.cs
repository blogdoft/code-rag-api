using BlogDoFT.Libs.DapperUtils.Abstractions.Extensions;
using BlogDoFT.Libs.DapperUtils.Postgres;
using CodeRag.Application.CodeQueries;
using Dapper;
using Npgsql;
using Pgvector;

namespace CodeRag.Infrastructure.Database.CodeQueries;

public sealed class CodeDocumentsRepository(NpgsqlDataSource dataSource) : ICodeDocumentsRepository
{
    public async Task<IEnumerable<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        double? minSimilarity,
        KindFilterOperator? kindOperator = null,
        string? kindValue = null,
        NamespaceFilterOperator? namespaceOperator = null,
        string? namespaceValue = null,
        TypeNameFilterOperator? typeNameOperator = null,
        string? typeNameValue = null,
        CancellationToken cancellationToken = default)
    {
        var where = new WhereBuilder();
        if (kindOperator is not null && kindValue is not null)
        {
            where.AndWith(kindValue, KindCondition(kindOperator.Value));
        }

        if (namespaceOperator is not null && namespaceValue is not null)
        {
            where.AndWith(namespaceValue, NamespaceCondition(namespaceOperator.Value));
        }

        if (typeNameOperator is not null && typeNameValue is not null)
        {
            where.AndWith(typeNameValue, TypeNameCondition(typeNameOperator.Value));
        }

        var newFilters = where.Build().ToString();

        // Build() returns "" when none of the 3 filters above were added, or "where (cond1)  and
        // (cond2) ...". The "where " keyword is dropped and what remains is spliced in front of
        // the fixed conditions below, so the new filters always come first in the WHERE clause.
        var newFiltersPrefix = newFilters.Length > 0
            ? newFilters["where ".Length..] + " and "
            : string.Empty;

        // The interpolated fragment is limited to fixed, developer-controlled column names and
        // SQL keywords chosen by KindCondition/NamespaceCondition/TypeNameCondition below - all
        // caller-supplied values still flow through Dapper parameters, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT cd.id AS Id
                 , cd.source_file AS SourceFile
                 , cd.kind AS Kind
                 , cd.type_name AS TypeName
                 , cd.member AS Member
                 , cd.embedding_text AS EmbeddingText
                 , ROUND((1 - (cd.embedding <=> @Embedding))::numeric, 10)::float8 AS Similarity
            FROM public.code_documents cd
            JOIN public.embedding_models em ON em.id = cd.embedding_model_id
            WHERE {newFiltersPrefix}cd.project_id = @ProjectId
              AND em.provider = @EmbeddingProvider
              AND em.model = @EmbeddingModel
              AND em.dimensions = @EmbeddingDimensions
              AND (1 - (cd.embedding <=> @Embedding)) >= @MinSimilarity
            ORDER BY cd.embedding <=> @Embedding
            LIMIT @Limit
            """;

        var parameters = new
        {
            ProjectId = projectId,
            EmbeddingProvider = embeddingProvider,
            EmbeddingModel = embeddingModel,
            EmbeddingDimensions = embeddingDimensions,
            Embedding = new Vector(queryEmbedding.ToArray()),
            Limit = limit,

            // Sentinel: no real cosine similarity is below double.MinValue, so omitting
            // minSimilarity makes this comparison a no-op instead of needing an "IS NULL OR" in SQL.
            MinSimilarity = minSimilarity ?? double.MinValue,

            // AsSqlWildCard (BlogDoFT.Libs.DapperUtils.Abstractions) turns the caller's '*' into
            // SQL's '%' for Contains/NotContains, so ILIKE @XValue is an exact (case-insensitive)
            // match unless the caller opts into a pattern. It doesn't escape a literal '%'/'_'
            // already in the value, so those still act as ILIKE wildcards too - an accepted
            // trade-off for reusing the shared helper instead of hand-rolling escaping.
            KindValue = kindOperator is KindFilterOperator.Contains ? kindValue?.AsSqlWildCard(toUpperCase: false) : kindValue,
            NamespaceValue = namespaceOperator is NamespaceFilterOperator.Contains or NamespaceFilterOperator.NotContains
                ? namespaceValue?.AsSqlWildCard(toUpperCase: false)
                : namespaceValue,
            TypeNameValue = typeNameOperator is TypeNameFilterOperator.Contains or TypeNameFilterOperator.NotContains
                ? typeNameValue?.AsSqlWildCard(toUpperCase: false)
                : typeNameValue,
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
#pragma warning restore S2077
        var rows = await connection.QueryAsync<CodeQueryResultRow>(command);

        return rows.Select(r => r.ToResult());
    }

    public async Task<bool> ExistsForProjectAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM public.code_documents WHERE project_id = @ProjectId)";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    private static string KindCondition(KindFilterOperator kindOperator) => kindOperator switch
    {
        KindFilterOperator.Contains => "cd.kind ILIKE @KindValue",
        KindFilterOperator.Equals => "cd.kind = @KindValue",
        KindFilterOperator.NotEquals => "cd.kind <> @KindValue",
        _ => throw new ArgumentOutOfRangeException(nameof(kindOperator), kindOperator, null),
    };

    private static string NamespaceCondition(NamespaceFilterOperator namespaceOperator) => namespaceOperator switch
    {
        NamespaceFilterOperator.Contains => "cd.namespace ILIKE @NamespaceValue",
        NamespaceFilterOperator.NotContains => "(cd.namespace IS NULL OR cd.namespace NOT ILIKE @NamespaceValue)",
        NamespaceFilterOperator.Equals => "cd.namespace = @NamespaceValue",
        NamespaceFilterOperator.NotEquals => "(cd.namespace IS NULL OR cd.namespace <> @NamespaceValue)",
        _ => throw new ArgumentOutOfRangeException(nameof(namespaceOperator), namespaceOperator, null),
    };

    private static string TypeNameCondition(TypeNameFilterOperator typeNameOperator) => typeNameOperator switch
    {
        TypeNameFilterOperator.Contains => "cd.type_name ILIKE @TypeNameValue",
        TypeNameFilterOperator.NotContains => "(cd.type_name IS NULL OR cd.type_name NOT ILIKE @TypeNameValue)",
        TypeNameFilterOperator.Equals => "cd.type_name = @TypeNameValue",
        _ => throw new ArgumentOutOfRangeException(nameof(typeNameOperator), typeNameOperator, null),
    };

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS SourceFile", ... aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record CodeQueryResultRow(
        long Id,
        string? SourceFile,
        string Kind,
        string? TypeName,
        string? Member,
        string EmbeddingText,
        double Similarity)
    {
        public CodeQueryResult ToResult() => new(Id, SourceFile, Kind, TypeName, Member, EmbeddingText, Similarity);
    }
#pragma warning restore SA1313
}
