using CodeRag.Application.CodeQueries;
using Dapper;
using Npgsql;
using Pgvector;

namespace CodeRag.Infrastructure.Database.CodeQueries;

public sealed class CodeDocumentsRepository(NpgsqlDataSource dataSource) : ICodeDocumentsRepository
{
    public async Task<IReadOnlyList<CodeQueryResult>> SearchAsync(
        long projectId,
        string embeddingProvider,
        string embeddingModel,
        int embeddingDimensions,
        IReadOnlyList<float> queryEmbedding,
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT cd.id AS Id
                 , cd.source_file AS SourceFile
                 , cd.kind AS Kind
                 , cd.type_name AS TypeName
                 , cd.member AS Member
                 , cd.embedding_text AS EmbeddingText
                 , ROUND((1 - (cd.embedding <=> @Embedding))::numeric, 10)::float8 AS Similarity
            FROM public.code_documents cd
            JOIN public.embedding_models em ON em.id = cd.embedding_model_id
            WHERE cd.project_id = @ProjectId
              AND em.provider = @EmbeddingProvider
              AND em.model = @EmbeddingModel
              AND em.dimensions = @EmbeddingDimensions
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
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CodeQueryResultRow>(command);

        return rows.Select(r => r.ToResult()).ToArray();
    }

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
}
