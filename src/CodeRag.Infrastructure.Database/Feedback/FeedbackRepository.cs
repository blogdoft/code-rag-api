using CodeRag.Application.Feedback;
using Dapper;
using Npgsql;

namespace CodeRag.Infrastructure.Database.Feedback;

public sealed class FeedbackRepository(NpgsqlDataSource dataSource) : IFeedbackRepository
{
    public async Task<FeedbackResult> InsertAsync(
        long projectId,
        string question,
        bool useful,
        IReadOnlyList<double> similarities,
        string? reason,
        string user,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.code_query_feedback (project_id, question, useful, similarities, reason, username)
            VALUES (@ProjectId, @Question, @Useful, @Similarities, @Reason, @Username)
            RETURNING id AS Id, project_id AS ProjectId, question AS Question, useful AS Useful,
                      similarities AS Similarities, reason AS Reason, username AS Username, created_at AS CreatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // First array-typed column in this repo: Npgsql/Dapper map double[] to Postgres'
        // float8[] natively, no extra type mapping/config needed.
        var command = new CommandDefinition(
            sql,
            new
            {
                ProjectId = projectId,
                Question = question,
                Useful = useful,
                Similarities = similarities.ToArray(),
                Reason = reason,
                Username = user,
            },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<FeedbackRow>(command);
        return row.ToFeedbackResult();
    }

    public async Task<bool> ExistsForProjectAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM public.code_query_feedback WHERE project_id = @ProjectId)";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    // A mutable POCO with property setters, not a positional record: Dapper's constructor-
    // matching fast path requires each constructor parameter's type to exactly equal
    // reader.GetFieldType(i), and Npgsql reports that as the generic System.Array for the
    // similarities float8[] column rather than double[] - which fails constructor matching even
    // though the actual returned value is a real double[]. Property-setter based mapping doesn't
    // have that restriction. S3459/S1144 are false positives here: Dapper populates every
    // property via reflection, invisible to the analyzer.
#pragma warning disable S3459, S1144
    private sealed class FeedbackRow
    {
        public long Id { get; set; }

        public long ProjectId { get; set; }

        public string Question { get; set; } = string.Empty;

        public bool Useful { get; set; }

        public double[] Similarities { get; set; } = [];

        public string? Reason { get; set; }

        public string Username { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        // code_query_feedback.created_at is stored as timestamptz (always UTC); Npgsql returns
        // it with Kind=Unspecified, so it must be stamped explicitly to serialize with a "Z" suffix.
        public FeedbackResult ToFeedbackResult() =>
            new(Id, ProjectId, Question, Useful, Similarities, Reason, Username, DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc));
    }
#pragma warning restore S3459, S1144
}
