using BlogDoFT.Libs.DapperUtils.Postgres;
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
        var where = new WhereBuilder().AndWith(nameFilter, "name ILIKE '%' || @NameFilter || '%'").Build();

        // The interpolated fragment is limited to a fixed, developer-controlled condition that
        // WhereBuilder either includes verbatim or omits entirely - the caller-supplied value
        // still flows through the @NameFilter Dapper parameter below, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT id AS Id, name AS Name, git_url AS GitUrl, git_raw_url AS GitRawUrl, created_at AS CreatedAt
            FROM public.projects
            {where}
            ORDER BY name
            """;
#pragma warning restore S2077

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

    public async Task<Project?> GetByIdAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id AS Id, name AS Name, git_url AS GitUrl, git_raw_url AS GitRawUrl, created_at AS CreatedAt
            FROM public.projects
            WHERE id = @ProjectId
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(command);
        return row?.ToProject();
    }

    public async Task<bool> NameExistsAsync(
        string name,
        long? excludingProjectId = null,
        CancellationToken cancellationToken = default)
    {
        var where = new WhereBuilder()
            .AndWith(name, "name = @Name")
            .AndWith(excludingProjectId, "id <> @ExcludingProjectId")
            .Build();

        // The interpolated fragment is limited to fixed, developer-controlled conditions that
        // WhereBuilder either includes verbatim or omits entirely - the caller-supplied values
        // still flow through Dapper parameters below, so this isn't injectable.
#pragma warning disable S2077
        var sql = $"""
            SELECT EXISTS(
                SELECT 1 FROM public.projects
                {where}
            )
            """;
#pragma warning restore S2077

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Name = name, ExcludingProjectId = excludingProjectId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<Project> InsertAsync(
        string name,
        string? gitUrl,
        string? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO public.projects (name, git_url, git_raw_url)
            VALUES (@Name, @GitUrl, @GitRawUrl)
            RETURNING id AS Id, name AS Name, git_url AS GitUrl, git_raw_url AS GitRawUrl, created_at AS CreatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Name = name, GitUrl = gitUrl, GitRawUrl = gitRawUrl },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleAsync<ProjectRow>(command);
        return row.ToProject();
    }

    public async Task<Project?> UpdateAsync(
        long projectId,
        string name,
        string? gitUrl,
        string? gitRawUrl,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE public.projects
            SET name = @Name, git_url = @GitUrl, git_raw_url = @GitRawUrl
            WHERE id = @ProjectId
            RETURNING id AS Id, name AS Name, git_url AS GitUrl, git_raw_url AS GitRawUrl, created_at AS CreatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { ProjectId = projectId, Name = name, GitUrl = gitUrl, GitRawUrl = gitRawUrl },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(command);
        return row?.ToProject();
    }

    public async Task<bool> DeleteAsync(long projectId, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM public.projects WHERE id = @ProjectId";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProjectId = projectId }, cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(command);
        return rowsAffected > 0;
    }

    // SA1313 wants these lower-case, but positional record parameters are also the record's
    // public properties - the standard .NET convention is PascalCase, matching the "AS Id",
    // "AS Name", ... aliases in the SQL above that Dapper binds them from.
#pragma warning disable SA1313
    private sealed record ProjectRow(long Id, string Name, string? GitUrl, string? GitRawUrl, DateTime CreatedAt)
    {
        // projects.created_at is stored as timestamptz (always UTC); Npgsql returns it with
        // Kind=Unspecified, so it must be stamped explicitly to serialize with a "Z" suffix.
        public Project ToProject() =>
            new(Id, Name, GitUrl, GitRawUrl, DateTime.SpecifyKind(CreatedAt, DateTimeKind.Utc));
    }
#pragma warning restore SA1313
}
