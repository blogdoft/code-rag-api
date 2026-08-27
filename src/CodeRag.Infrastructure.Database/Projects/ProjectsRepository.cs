using CodeRag.Application.Projects;
using Dapper;
using Npgsql;

namespace CodeRag.Infrastructure.Database.Projects;

public sealed class ProjectsRepository(NpgsqlDataSource dataSource) : IProjectsRepository
{
    public async Task<IEnumerable<Project>> SearchAsync(
        string? nameFilter,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, name AS Name, created_at AS CreatedAt
            FROM public.projects
            WHERE @NameFilter::text IS NULL OR name ILIKE '%' || @NameFilter || '%'
            ORDER BY name
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { NameFilter = nameFilter }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ProjectRow>(command);

        return rows.Select(r => r.ToProject());
    }

    public async Task<bool> ExistsAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM public.projects WHERE id = @ProjectId)";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS Name", "AS CreatedAt" aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record ProjectRow(long Id, string Name, DateTime CreatedAt)
    {
        // projects.created_at is stored as timestamptz (always UTC); Npgsql returns it with
        // Kind=Unspecified, so it must be stamped explicitly to serialize with a "Z" suffix.
        public Project ToProject() => new(Id, Name, DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc));
    }
#pragma warning restore SA1313
}
