using Npgsql;
using Testcontainers.PostgreSql;

namespace CodeRag.Api.Tests;

/// <summary>Boots the real API, backed by a disposable pgvector-enabled Postgres container, shared across <see cref="ApiCollection"/>.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("coderag_api_test")
        .WithUsername("coderag_api_test")
        .WithPassword("coderag_api_test")
        .Build();

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using (var setupConnection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await setupConnection.OpenAsync();
            await using var command = setupConnection.CreateCommand();
            command.CommandText = Schema.Ddl;
            await command.ExecuteNonQueryAsync();
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_container.GetConnectionString());
        dataSourceBuilder.UseVector();
        DataSource = dataSourceBuilder.Build();

        Factory = new CustomWebApplicationFactory(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}
