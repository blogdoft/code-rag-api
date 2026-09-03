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

    public async Task<IReadOnlyList<WeeklyFeedbackStats>> GetStatsAsync(
        DateTime startDate,
        DateTime endDate,
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        // Dense week x project grid: "weeks" enumerates every ISO calendar week (Monday - Postgres'
        // date_trunc('week', ...) truncates to Monday) overlapping the window, "eligible_projects"
        // is either every registered project or the single one matching projectId, and the CROSS
        // JOIN + LEFT JOIN guarantees every (week, project) pair appears at least once, zero-filled
        // when there's no matching feedback.
        const string sql = """
            WITH weeks AS (
                SELECT generate_series(
                    date_trunc('week', @StartDate::timestamptz),
                    date_trunc('week', @EndDate::timestamptz),
                    interval '7 days'
                ) AS week_start
            ),
            eligible_projects AS (
                SELECT id, name FROM public.projects
                WHERE (@ProjectId::int8 IS NULL OR id = @ProjectId)
            )
            SELECT
                w.week_start AS WeekStart,
                p.id AS ProjectId,
                p.name AS ProjectName,
                COUNT(f.id) AS TotalCount,
                COUNT(f.id) FILTER (WHERE f.useful) AS UsefulCount,
                COUNT(f.id) FILTER (WHERE NOT f.useful) AS NotUsefulCount
            FROM weeks w
            CROSS JOIN eligible_projects p
            LEFT JOIN public.code_query_feedback f
                ON f.project_id = p.id
                AND f.created_at >= @StartDate AND f.created_at <= @EndDate
                AND date_trunc('week', f.created_at) = w.week_start
            GROUP BY w.week_start, p.id, p.name
            ORDER BY w.week_start, p.id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { StartDate = startDate, EndDate = endDate, ProjectId = projectId },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<StatsRow>(command);

        // GroupBy preserves source order (per SQL's ORDER BY) both across groups and within each
        // group, so no explicit re-sort is needed here.
        return rows
            .GroupBy(row => row.WeekStart)
            .Select(group =>
            {
                // DateOnly.FromDateTime only copies the Y/M/D components - the DateTimeKind
                // Npgsql assigns to a timestamptz column (Unspecified) doesn't matter here,
                // unlike full-datetime serialization elsewhere in this repo (see CreatedAt below).
                var weekStart = DateOnly.FromDateTime(group.Key);
                return new WeeklyFeedbackStats(weekStart, weekStart.AddDays(6), group.Select(row => row.ToProjectFeedbackStats()).ToList());
            })
            .ToList();
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

    // Positional record is safe here (unlike FeedbackRow above): every column is a scalar type
    // (timestamptz, int8, text, bigint) whose reader.GetFieldType(i) matches the constructor
    // parameter type exactly, so Dapper's constructor-matching fast path applies without issue.
    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase.
#pragma warning disable SA1313
    private sealed record StatsRow(
        DateTime WeekStart,
        long ProjectId,
        string ProjectName,
        long TotalCount,
        long UsefulCount,
        long NotUsefulCount)
    {
        public ProjectFeedbackStats ToProjectFeedbackStats() => new(
            ProjectId,
            ProjectName,
            TotalCount,
            UsefulCount,
            NotUsefulCount,
            TotalCount == 0 ? 0 : Math.Round((double)UsefulCount / TotalCount * 100, 2),
            TotalCount == 0 ? 0 : Math.Round((double)NotUsefulCount / TotalCount * 100, 2));
    }
#pragma warning restore SA1313
}
